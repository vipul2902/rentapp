namespace RentApp.Application.Common.Errors;

/// <summary>Raised by the persistence layer when an insert/update hits a unique constraint (e.g. a race on email).</summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception($"Unique constraint '{constraintName}' was violated.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
