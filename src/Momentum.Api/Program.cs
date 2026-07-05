using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Momentum.Api.ExceptionHandling;
using Momentum.Api.Security;
using Momentum.Infrastructure;
using Momentum.Infrastructure.Persistence;
using Momentum.SharedKernel.Modules;
using Momentum.SharedKernel.Security;
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

    // ---- Infrastructure (DbContext, Identity core, base health checks) ----
    builder.Services.AddInfrastructure(builder.Configuration);

    // ---- Current-user accessor: one HttpContext-backed instance serving both
    // the strict module-facing ICurrentUser and the lenient ICurrentUserAccessor
    // that AppDbContext's global query filter uses. ----
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<CurrentUser>();
    builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
    builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(sp => sp.GetRequiredService<CurrentUser>()));

    // ---- Module discovery & registration ----
    // Phase 1 ships no modules yet, so this discovers an empty list — the loop
    // below is future-proofing, not dead code. See ModuleAssemblyScanner's doc
    // comment for how a new module gets picked up with zero changes here.
    var enabledModules = builder.Configuration["Modules:Enabled"]
        ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var modules = ModuleAssemblyScanner.DiscoverModules(enabledModules);

    Log.Information(
        "Discovered {ModuleCount} module(s): {Modules}",
        modules.Count,
        modules.Select(m => m.Name).ToArray());

    foreach (var module in modules)
    {
        module.AddServices(builder.Services, builder.Configuration);
    }

    // FluentValidation: scan SharedKernel + every module assembly for IValidator<T> implementations.
    builder.Services.AddValidatorsFromAssembly(typeof(IModule).Assembly);
    foreach (var moduleAssembly in ModuleAssemblyScanner.GetModuleAssemblies())
    {
        builder.Services.AddValidatorsFromAssembly(moduleAssembly);
    }

    // ---- Error handling (RFC 7807 ProblemDetails everywhere) ----
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<AppExceptionHandler>();

    // ---- AuthN/AuthZ ----
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtSecret = jwtSection["Secret"]
        ?? throw new InvalidOperationException("Missing configuration 'Jwt:Secret' (env: Jwt__Secret).");

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

    // ---- Rate limiting (modules apply .RequireRateLimiting("auth") to sensitive endpoints) ----
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("auth", limiterOptions =>
        {
            limiterOptions.PermitLimit = 10;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 0;
        });
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

    app.UseSerilogRequestLogging();

    app.UseExceptionHandler();

    // Swagger is left on in every environment: this is a personal, non-public app
    // meant to sit behind its own auth and network controls, and the brief asks
    // to "show it running behind Swagger".
    app.UseSwagger();
    app.UseSwaggerUI();

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

    foreach (var module in modules)
    {
        module.MapEndpoints(app);
    }

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
