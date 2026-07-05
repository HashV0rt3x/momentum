using System.Net;
using Xunit;

namespace Momentum.IntegrationTests;

/// <summary>
/// Phase 1 smoke test: proves the WebApplicationFactory + Testcontainers Postgres
/// scaffold actually boots the app (Serilog, module discovery loop, DbContext,
/// migrations, health checks) end to end. The user-isolation test and v1-loop
/// tests land in later phases using this same factory.
/// </summary>
public sealed class HealthEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Live_endpoint_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_endpoint_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_document_is_served()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
