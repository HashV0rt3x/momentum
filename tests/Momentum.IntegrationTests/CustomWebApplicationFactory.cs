using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Momentum.IntegrationTests;

/// <summary>
/// Spins up a real, throwaway Postgres via Testcontainers for every test run —
/// deliberately not mocking the database, since EF Core global query filters and
/// Npgsql-specific behavior are exactly what the user-isolation test needs to
/// exercise for real. Requires a working Docker daemon wherever `dotnet test` runs.
///
/// Shared across every test class in this assembly via IClassFixture; each test
/// class gets its own client but the same running container. Applies migrations
/// through Program.cs's normal guarded-startup path (Database:MigrateOnStartup).
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("momentum_test")
        .WithUsername("momentum")
        .WithPassword("momentum")
        .Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["Jwt:Secret"] = "test-only-secret-at-least-32-bytes-long-000000",
                ["Jwt:Issuer"] = "momentum-api-tests",
                ["Jwt:Audience"] = "momentum-client-tests",
                ["Database:MigrateOnStartup"] = "true",
            });
        });
    }
}
