namespace RentApp.Application.Common.Errors;

/// <summary>
/// Base for expected, client-facing failures. The message must be safe to show to an end user;
/// the API layer maps each subtype to an HTTP status code.
/// </summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code, string message) : AppException(code, message);

public sealed class ConflictException(string code, string message) : AppException(code, message);

public sealed class ForbiddenException(string code, string message) : AppException(code, message);

public sealed class UnauthorizedException(string code, string message) : AppException(code, message);

public sealed class ValidationException(
    string code,
    string message,
    IReadOnlyDictionary<string, string[]>? errors = null) : AppException(code, message)
{
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;
}
