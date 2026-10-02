using System.Diagnostics;

namespace RentApp.Api.Middleware;

/// <summary>
/// One structured log line per request: method, path (no query string, which may hold search terms such
/// as phone numbers), status and duration. User/organization ids join the log scope once auth exists.
/// </summary>
internal sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
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
            var level = context.Request.Path.StartsWithSegments("/health") ? LogLevel.Debug
                : statusCode >= StatusCodes.Status500InternalServerError ? LogLevel.Error
                : LogLevel.Information;

            if (logger.IsEnabled(level))
            {
                var method = context.Request.Method;
                var path = context.Request.Path.Value ?? "/";
                var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                LogRequest(logger, level, method, path, statusCode, elapsedMs);
            }
        }
    }

    [LoggerMessage(Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F1} ms")]
    private static partial void LogRequest(ILogger logger, LogLevel level, string method, string path, int statusCode, double elapsedMs);
}
