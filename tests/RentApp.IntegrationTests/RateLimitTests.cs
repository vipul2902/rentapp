using System.Net;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class RateLimitTests(ContainersFixture containers)
{
    [Fact]
    public async Task AuthEndpointsAreRateLimitedWithStandardError()
    {
        await using var factory = RentAppFactory.For(containers, new Dictionary<string, string>
        {
            ["RateLimiting:Auth:PermitLimit"] = "3",
            ["RateLimiting:Auth:WindowSeconds"] = "60",
        });
        using var client = factory.CreateClient();

        var attempts = new List<HttpResponseMessage>();
        for (var i = 0; i < 4; i++)
        {
            attempts.Add(await client.LoginAsync(UniqueEmail("brute"), "Wrong-Pass-123"));
        }

        Assert.All(attempts.Take(3), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        var limited = attempts[3];
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("RATE_LIMITED", (await limited.ReadErrorAsync()).Code);
        Assert.True(limited.Headers.RetryAfter is not null || limited.Headers.Contains("Retry-After"));

        // Health checks are not affected by the auth limiter.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health/live", UriKind.Relative))).StatusCode);
    }
}
