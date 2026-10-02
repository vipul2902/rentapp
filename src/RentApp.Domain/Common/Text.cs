namespace RentApp.Domain.Common;

internal static class Text
{
    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
