using Microsoft.AspNetCore.Diagnostics;
using RentApp.Application.Common.Errors;

namespace RentApp.Api.Errors;

/// <summary>
/// Converts exceptions into <see cref="ApiError"/> responses. Expected failures (<see cref="AppException"/>)
/// keep their safe message; anything else becomes a generic 500 and is logged with full details server-side only.
/// </summary>
internal sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is nobody to send a body to.
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var (statusCode, error) = Map(exception, httpContext.TraceIdentifier);
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception);
        }
        else
        {
            LogExpectedFailure(logger, error.Code, statusCode);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }

    internal static (int StatusCode, ApiError Error) Map(Exception exception, string traceId) => exception switch
    {
        ValidationException e => (StatusCodes.Status400BadRequest, new ApiError(e.Code, e.Message, traceId) { Errors = e.Errors }),
        NotFoundException e => (StatusCodes.Status404NotFound, new ApiError(e.Code, e.Message, traceId)),
        ConflictException e => (StatusCodes.Status409Conflict, new ApiError(e.Code, e.Message, traceId)),
        ForbiddenException e => (StatusCodes.Status403Forbidden, new ApiError(e.Code, e.Message, traceId)),
        UnauthorizedException e => (StatusCodes.Status401Unauthorized, new ApiError(e.Code, e.Message, traceId)),
        AppException e => (StatusCodes.Status400BadRequest, new ApiError(e.Code, e.Message, traceId)),
        BadHttpRequestException e => (e.StatusCode, ApiErrors.ForStatus(e.StatusCode, traceId)),
        _ => (StatusCodes.Status500InternalServerError, ApiErrors.ForStatus(StatusCodes.Status500InternalServerError, traceId)),
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing request")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request failed with {ErrorCode} ({StatusCode})")]
    private static partial void LogExpectedFailure(ILogger logger, string errorCode, int statusCode);
}
