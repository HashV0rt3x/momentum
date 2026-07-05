using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Momentum.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` / `dotnet ef database update` construct an
/// AppDbContext without spinning up the full Momentum.Api host (which would need
/// JWT secrets, module discovery, etc. just to generate a migration). Reads the
/// same ConnectionStrings__Default env var the running app uses; falls back to a
/// local-dev default so migrations can be authored without exporting anything.
///
/// Run from the repo root:
///   dotnet ef migrations add &lt;Name&gt; --project src/Momentum.Infrastructure --startup-project src/Momentum.Api
///   dotnet ef database update --project src/Momentum.Infrastructure --startup-project src/Momentum.Api
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDevDefault = "Host=localhost;Port=5432;Database=momentum;Username=momentum;Password=momentum";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? LocalDevDefault;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        return new AppDbContext(optionsBuilder.Options, new NullCurrentUserAccessor(), TimeProvider.System);
    }
}
