using System.Net;
using System.Net.Http.Json;
using RentApp.IntegrationTests.Infrastructure;

namespace RentApp.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class ReadinessFailureTests(ContainersFixture containers)
{
    [Fact]
    public async Task ReadinessReturns503WhenRedisIsUnreachableButApiStillServes()
    {
        // Port 1 on loopback: nothing listens there, so Redis never connects.
        await using var factory = new RentAppFactory(containers.Postgres.GetConnectionString(), "127.0.0.1:1");
        using var client = factory.CreateClient();

        var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        var body = await ready.Content.ReadFromJsonAsync<HealthBody>();
        var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Contains(body!.Checks, c => c is { Name: "redis", Status: "Unhealthy" });
        Assert.Contains(body.Checks, c => c is { Name: "postgres", Status: "Healthy" });
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    private sealed record HealthBody(string Status, IReadOnlyList<HealthCheck> Checks);

    private sealed record HealthCheck(string Name, string Status);
}
