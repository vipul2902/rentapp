using System.Diagnostics;
using RentApp.Application.Common.Security;

namespace RentApp.Api.Middleware;

/// <summary>
/// One structured log line per request: method, path (no query string, which may hold search terms such
/// as phone numbers), status, duration, and the user/organization ids once authentication has run.
/// </summary>
internal sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private const string Anonymous = "-";

    /// <summary>Requests slower than this are logged as warnings, to spot slow queries in production.</summary>
    internal const double SlowRequestMs = 1_000;

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var statusCode = context.Response.StatusCode;
            var slow = Stopwatch.GetElapsedTime(started).TotalMilliseconds > SlowRequestMs;
            var level = statusCode >= StatusCodes.Status500InternalServerError ? LogLevel.Error
                : slow ? LogLevel.Warning
                : context.Request.Path.StartsWithSegments("/health") ? LogLevel.Debug
                : LogLevel.Information;

            if (logger.IsEnabled(level))
            {
                var method = context.Request.Method;
                var path = context.Request.Path.Value ?? "/";
                var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var userId = context.User.FindFirst(AppClaimTypes.Subject)?.Value ?? Anonymous;
                var organizationId = context.User.FindFirst(AppClaimTypes.Organization)?.Value ?? Anonymous;
                LogRequest(logger, level, method, path, statusCode, elapsedMs, userId, organizationId);
            }
        }
    }

    [LoggerMessage(Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F1} ms (user {UserId}, org {OrganizationId})")]
    private static partial void LogRequest(
        ILogger logger, LogLevel level, string method, string path, int statusCode, double elapsedMs, string userId, string organizationId);
}
