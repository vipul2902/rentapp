using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentApp.Infrastructure.Persistence;
using RentApp.IntegrationTests.Infrastructure;

namespace RentApp.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class FoundationTests(ContainersFixture containers) : IAsyncLifetime
{
    private const string CorrelationHeader = "X-Correlation-ID";
    private RentAppFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = RentAppFactory.For(containers);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task LivenessIsHealthyWithoutCheckingDependencies()
    {
        var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));
        var body = await response.Content.ReadFromJsonAsync<HealthBody>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body!.Status);
        Assert.Empty(body.Checks);
    }

    [Fact]
    public async Task ReadinessReportsPostgresAndRedisHealthy()
    {
        var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        var body = await response.Content.ReadFromJsonAsync<HealthBody>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body!.Status);
        Assert.Contains(body.Checks, c => c is { Name: "postgres", Status: "Healthy" });
        Assert.Contains(body.Checks, c => c is { Name: "redis", Status: "Healthy" });
    }

    [Fact]
    public async Task MigrationsAreAppliedOnStartup()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_InitialBaseline", StringComparison.Ordinal));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task UnknownRouteReturnsStandardErrorShapeWithTraceId()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative));
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.Equal(response.Headers.GetValues(CorrelationHeader).Single(), error.TraceId);
    }

    [Fact]
    public async Task ClientCorrelationIdIsEchoedAndUsedAsTraceId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/does-not-exist");
        request.Headers.Add(CorrelationHeader, "mobile-req-42");

        var response = await _client.SendAsync(request);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();

        Assert.Equal("mobile-req-42", response.Headers.GetValues(CorrelationHeader).Single());
        Assert.Equal("mobile-req-42", error!.TraceId);
    }

    [Fact]
    public async Task MalformedCorrelationIdIsReplaced()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(CorrelationHeader, "<script>alert(1)</script>");

        var response = await _client.SendAsync(request);

        var echoed = response.Headers.GetValues(CorrelationHeader).Single();
        Assert.DoesNotContain("<", echoed, StringComparison.Ordinal);
        Assert.Equal(32, echoed.Length);
    }

    private sealed record HealthBody(string Status, IReadOnlyList<HealthCheck> Checks);

    private sealed record HealthCheck(string Name, string Status);

    private sealed record ErrorBody(string Code, string Message, string TraceId);
}
