namespace RentApp.Api.Middleware;

/// <summary>
/// Accepts a client-supplied X-Correlation-ID (if well-formed) or generates one, uses it as the request's
/// TraceIdentifier (so it appears as traceId in error bodies), echoes it back, and adds it to the log scope.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaxLength = 64;

    private static readonly Func<ILogger, string, IDisposable?> CorrelationScope =
        LoggerMessage.DefineScope<string>("CorrelationId:{CorrelationId}");

    public async Task InvokeAsync(HttpContext context)
    {
        string? supplied = context.Request.Headers[HeaderName];
        var correlationId = IsValid(supplied) ? supplied! : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (CorrelationScope(logger, correlationId))
        {
            await next(context);
        }
    }

    internal static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
