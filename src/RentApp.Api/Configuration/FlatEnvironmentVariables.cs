namespace RentApp.Api.Configuration;

/// <summary>
/// Maps the flat variable names documented in .env.example (DATABASE_CONNECTION_STRING, JWT_SECRET, ...)
/// onto the hierarchical configuration keys the app binds to. Added last, so these win over appsettings.
/// </summary>
internal static class FlatEnvironmentVariables
{
    private static readonly (string Variable, string Key)[] Mappings =
    [
        ("DATABASE_CONNECTION_STRING", "ConnectionStrings:Database"),
        ("REDIS_CONNECTION_STRING", "ConnectionStrings:Redis"),
        ("JWT_SECRET", "Jwt:Secret"),
        ("JWT_ISSUER", "Jwt:Issuer"),
        ("JWT_AUDIENCE", "Jwt:Audience"),
    ];

    public static IConfigurationBuilder AddFlatEnvironmentVariables(this IConfigurationBuilder builder) =>
        builder.AddInMemoryCollection(Map(Environment.GetEnvironmentVariable));

    internal static Dictionary<string, string?> Map(Func<string, string?> getVariable)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (variable, key) in Mappings)
        {
            var value = getVariable(variable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[key] = value;
            }
        }

        var origins = getVariable("CORS_ALLOWED_ORIGINS")?
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        for (var i = 0; i < origins.Length; i++)
        {
            values[$"Cors:AllowedOrigins:{i}"] = origins[i];
        }

        return values;
    }
}
