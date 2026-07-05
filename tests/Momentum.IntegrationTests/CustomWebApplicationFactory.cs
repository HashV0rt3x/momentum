using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Momentum.IntegrationTests;

/// <summary>
/// Spins up a real, throwaway Postgres via Testcontainers for every test run —
/// deliberately not mocking the database, since EF Core global query filters and
/// Npgsql-specific behavior are exactly what the user-isolation tests need to
/// exercise for real. Requires a working Docker daemon wherever `dotnet test` runs.
///
/// Overrides are applied as PROCESS ENVIRONMENT VARIABLES, not via
/// ConfigureAppConfiguration: with minimal hosting (WebApplication.CreateBuilder),
/// factory-supplied configuration lands in the *host* configuration, which the
/// app's own appsettings.{Environment}.json files override. Environment variables
/// are added after those JSON files, so they are the only override that reliably
/// wins. Safe here because the whole test assembly shares one process and one
/// database container.
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

        // Must be set before the (lazy) host build, i.e. before the first CreateClient().
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", "test-only-secret-at-least-32-bytes-long-000000");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "momentum-api-tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "momentum-client-tests");
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "true");
        Environment.SetEnvironmentVariable("Telegram__BotToken", "0000000000:test-bot-token-not-real");
        Environment.SetEnvironmentVariable("Telegram__EnablePolling", "false");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}
