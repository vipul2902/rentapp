using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RentApp.Application.Common.Errors;

namespace RentApp.Api.Errors;

/// <summary>Builds <see cref="ApiError"/> bodies for framework-generated responses (404s, 405s, model binding, ...).</summary>
internal static class ApiErrors
{
    private const string InvalidValueMessage = "The value is invalid.";

    public static ApiError ForStatus(int statusCode, string traceId)
    {
        var (code, message) = statusCode switch
        {
            StatusCodes.Status400BadRequest => (ErrorCodes.BadRequest, "The request could not be processed. Please check it and try again."),
            StatusCodes.Status401Unauthorized => (ErrorCodes.Unauthorized, "Please sign in to continue."),
            StatusCodes.Status403Forbidden => (ErrorCodes.Forbidden, "You do not have permission to do this."),
            StatusCodes.Status404NotFound => (ErrorCodes.NotFound, "The requested resource was not found."),
            StatusCodes.Status405MethodNotAllowed => (ErrorCodes.MethodNotAllowed, "This action is not supported."),
            StatusCodes.Status409Conflict => (ErrorCodes.Conflict, "This change conflicts with the current data. Please refresh and try again."),
            StatusCodes.Status429TooManyRequests => (ErrorCodes.RateLimited, "Too many attempts. Please wait a moment and try again."),
            StatusCodes.Status503ServiceUnavailable => (ErrorCodes.ServiceUnavailable, "The service is temporarily unavailable. Please try again shortly."),
            >= 500 => (ErrorCodes.InternalError, "Something went wrong on our side. Please try again."),
            _ => (ErrorCodes.BadRequest, "The request could not be processed. Please check it and try again."),
        };

        return new ApiError(code, message, traceId);
    }

    /// <summary>Used with UseStatusCodePages: fills in a body for error responses that have none.</summary>
    public static Task WriteStatusCodeBody(StatusCodeContext context)
    {
        var httpContext = context.HttpContext;
        return httpContext.Response.WriteAsJsonAsync(ForStatus(httpContext.Response.StatusCode, httpContext.TraceIdentifier));
    }

    /// <summary>Replaces the default ValidationProblemDetails for [ApiController] model validation failures.</summary>
    public static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error =>
                        // JSON deserialization messages ("$.amount ... System.Decimal") reveal internals; keep them generic.
                        entry.Key.StartsWith('$') || string.IsNullOrWhiteSpace(error.ErrorMessage) || error.Exception is not null
                            ? InvalidValueMessage
                            : error.ErrorMessage)
                    .Distinct()
                    .ToArray());

        return new BadRequestObjectResult(new ApiError(
            ErrorCodes.ValidationFailed,
            "Some fields are missing or invalid. Please check and try again.",
            context.HttpContext.TraceIdentifier) { Errors = errors });
    }
}
