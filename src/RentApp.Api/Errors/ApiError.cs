using System.Text.Json.Serialization;

namespace RentApp.Api.Errors;

/// <summary>The single error shape returned by every endpoint. Never contains stack traces or internal details.</summary>
public sealed record ApiError(string Code, string Message, string TraceId)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
