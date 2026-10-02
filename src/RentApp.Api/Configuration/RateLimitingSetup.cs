using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using RentApp.Api.Errors;

namespace RentApp.Api.Configuration;

/// <summary>Bound from RateLimiting:Auth. Limits sign-up/sign-in/refresh attempts per client IP.</summary>
public sealed class AuthRateLimitOptions
{
    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;
}

/// <summary>
/// In-memory, per-instance limits; sufficient while the API runs as a single instance. Move the
/// partition store to Redis before scaling out. Behind a proxy, forwarded headers must be configured
/// so RemoteIpAddress is the client, not the proxy.
/// </summary>
internal static class RateLimitingSetup
{
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthRateLimitOptions>().Bind(configuration.GetSection("RateLimiting:Auth"));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiErrors.ForStatus(StatusCodes.Status429TooManyRequests, context.HttpContext.TraceIdentifier), cancellationToken);
            };

            options.AddPolicy(AuthPolicy, httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }
}
