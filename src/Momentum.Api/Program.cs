using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Momentum.Api.Endpoints;
using Momentum.Api.ExceptionHandling;
using Momentum.Api.Security;
using Momentum.Application.Common.Security;
using Momentum.Application.Users;
using Momentum.Infrastructure;
using Momentum.Infrastructure.Persistence;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;

// Bootstrap logger: catches anything that goes wrong before the full Serilog
// pipeline (which needs configuration) is wired up.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Momentum.Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .WriteTo.Console(new CompactJsonFormatter()));

    // ---- Infrastructure (DbContext, Identity core, auth/user services, base health checks) ----
    builder.Services.AddInfrastructure(builder.Configuration);

    // ---- Current-user accessor: one HttpContext-backed instance serving both
    // the strict application-facing ICurrentUser and the lenient
    // ICurrentUserAccessor that AppDbContext's global query filter uses. ----
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<CurrentUser>();
    builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
    builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(sp => sp.GetRequiredService<CurrentUser>()));

    // FluentValidation: scan the Application assembly for IValidator<T> implementations.
    builder.Services.AddValidatorsFromAssembly(typeof(IUserService).Assembly);

    // ---- Error handling (spec error shape everywhere) ----
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<AppExceptionHandler>();

    // ---- AuthN/AuthZ ----
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtSecret = jwtSection["Secret"]
        ?? throw new InvalidOperationException("Missing configuration 'Jwt:Secret' (env: Jwt__Secret).");

    // Fail fast at startup instead of at first token signing: HS256 requires a key
    // of at least 256 bits (32 bytes).
    if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    {
        throw new InvalidOperationException(
            "'Jwt:Secret' must be at least 32 bytes (256 bits) for HMAC-SHA256. Generate one with: openssl rand -base64 48");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSection["Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddAuthorization();

    // ---- CORS (strict by default: empty Cors__Origins allows nothing) ----
    var corsOrigins = (builder.Configuration["Cors:Origins"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy =>
        {
            if (corsOrigins.Length > 0)
            {
                policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }
        });
    });

    // ---- Reverse-proxy awareness: the container serves plain HTTP behind a
    // TLS-terminating proxy, so trust X-Forwarded-For/Proto — otherwise
    // UseHttpsRedirection loops and the rate limiter / logs see the proxy's IP
    // instead of the client's. KnownNetworks/Proxies are cleared because the
    // proxy's address isn't known here; restrict them if it ever is. ----
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ---- Rate limiting (auth endpoints apply .RequireRateLimiting("auth")).
    // Partitioned per client IP: one abusive client must not exhaust the login
    // budget for everyone. ----
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }));
    });

    // ---- Health checks (liveness has no dependencies; readiness checks Postgres — added in AddInfrastructure) ----
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

    // ---- Observability ----
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Momentum API", Version = "v1" });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste only the JWT access token — Swagger adds the 'Bearer ' prefix automatically.",
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                },
                Array.Empty<string>()
            },
        });
    });

    var app = builder.Build();

    // ---- Guarded startup migration. Set Database__MigrateOnStartup=false to run
    // migrations as a separate init step (e.g. a K8s Job / CI step) instead. ----
    if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Log.Information("Applying database migrations...");
        await db.Database.MigrateAsync();
        Log.Information("Database migrations applied.");
    }

    // Must run before anything that inspects scheme or client IP
    // (HTTPS redirection, HSTS, rate limiting, request logging).
    app.UseForwardedHeaders();

    app.UseSerilogRequestLogging();

    app.UseExceptionHandler();

    // Swagger: on in Development, off in Production unless explicitly enabled
    // via Swagger__Enabled=true (handy on a private server behind a proxy).
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", defaultValue: false))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.UseCors("Default");

    app.UseHttpMetrics();

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    });

    app.MapMetrics("/metrics");

    // ---- API v1 (spec base path) ----
    var api = app.MapGroup("/api/v1");
    AuthEndpoints.Map(api);
    UserEndpoints.Map(api);
    SettingsEndpoints.Map(api);
    CategoryEndpoints.Map(api);
    ProjectEndpoints.Map(api);
    TaskEndpoints.Map(api);
    FocusSessionEndpoints.Map(api);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Momentum.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed for WebApplicationFactory&lt;Program&gt; in integration tests.</summary>
public partial class Program;
