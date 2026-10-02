using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RentApp.Infrastructure;

namespace RentApp.Api.Health;

/// <summary>
/// /health/live — the process is up (no dependency checks).
/// /health/ready — PostgreSQL and Redis are reachable; returns 503 otherwise.
/// Responses list check names and status only; failure details stay in the server logs.
/// </summary>
internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse,
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DependencyInjection.ReadyTag),
            ResponseWriter = WriteResponse,
        }).AllowAnonymous();

        return app;
    }

    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        var body = new HealthResponse(
            report.Status.ToString(),
            Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            [.. report.Entries.Select(e => new HealthCheckEntry(e.Key, e.Value.Status.ToString(), Math.Round(e.Value.Duration.TotalMilliseconds, 1)))]);

        return context.Response.WriteAsJsonAsync(body);
    }

    internal sealed record HealthResponse(string Status, double TotalDurationMs, IReadOnlyList<HealthCheckEntry> Checks);

    internal sealed record HealthCheckEntry(string Name, string Status, double DurationMs);
}
