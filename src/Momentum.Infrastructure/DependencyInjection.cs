using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Momentum.Application.Auth;
using Momentum.Application.Categories;
using Momentum.Application.Focus;
using Momentum.Application.Projects;
using Momentum.Application.Settings;
using Momentum.Application.Tasks;
using Momentum.Application.Users;
using Momentum.Infrastructure.Auth;
using Momentum.Infrastructure.Categories;
using Momentum.Infrastructure.Focus;
using Momentum.Infrastructure.Identity;
using Momentum.Infrastructure.Persistence;
using Momentum.Infrastructure.Projects;
using Momentum.Infrastructure.Settings;
using Momentum.Infrastructure.Tasks;
using Momentum.Infrastructure.Users;

namespace Momentum.Infrastructure;

/// <summary>
/// Wires the DbContext, Identity core, auth/user services, and
/// infrastructure-level health checks. Called once from Momentum.Api's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Missing configuration 'ConnectionStrings:Default' (env: ConnectionStrings__Default).");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // Application services that need the current user's id do so via
        // Momentum.Application.Common.Security.ICurrentUser (throws if
        // unauthenticated). This lower-level, nullable accessor exists only so
        // AppDbContext's global query filter also works with no HTTP context
        // (design-time, workers). Momentum.Api registers the real
        // HttpContext-backed implementation, which overrides this default.
        services.TryAddScoped<ICurrentUserAccessor, NullCurrentUserAccessor>();

        services.TryAddSingleton(TimeProvider.System);

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false; // Telegram-only sign-in; email is optional profile data.
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // ---- Options ----
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));

        // ---- Auth & user services ----
        services.AddSingleton<ITelegramLoginVerifier, TelegramLoginVerifier>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // ---- Feature services ----
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IFocusSessionService, FocusSessionService>();

        // ---- Telegram bot: /start -> 6-digit login code (long polling) ----
        services.AddHttpClient();
        services.AddHostedService<TelegramBotPollingService>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

        return services;
    }
}
