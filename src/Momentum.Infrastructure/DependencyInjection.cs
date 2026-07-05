using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Momentum.Infrastructure.Identity;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure;

/// <summary>
/// Wires the single DbContext, Identity core, and infrastructure-level health
/// checks. Called once from Momentum.Api's Program.cs, before modules'
/// AddServices() run (modules depend on AppDbContext / UserManager being
/// registered already).
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

        // Modules that need the current user's id for building queries do so via
        // Momentum.SharedKernel.Security.ICurrentUser (throws if unauthenticated).
        // This lower-level, nullable accessor exists only so AppDbContext's global
        // query filter also works with no HTTP context (design-time, workers).
        // Momentum.Api registers the real HttpContext-backed implementation, which
        // overrides this default for the web host.
        services.TryAddScoped<ICurrentUserAccessor, NullCurrentUserAccessor>();

        services.TryAddSingleton(TimeProvider.System);

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

        return services;
    }
}
