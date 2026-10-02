namespace RentApp.Infrastructure.Configuration;

/// <summary>Bound from the <c>ConnectionStrings</c> configuration section.</summary>
public sealed class ConnectionStringOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Database { get; set; } = string.Empty;

    public string Redis { get; set; } = string.Empty;
}
