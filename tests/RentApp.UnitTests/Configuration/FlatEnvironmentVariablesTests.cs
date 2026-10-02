using RentApp.Api.Configuration;

namespace RentApp.UnitTests.Configuration;

public class FlatEnvironmentVariablesTests
{
    [Fact]
    public void MapsDocumentedVariablesToConfigurationKeys()
    {
        var env = new Dictionary<string, string>
        {
            ["DATABASE_CONNECTION_STRING"] = "Host=db",
            ["REDIS_CONNECTION_STRING"] = "redis:6379",
            ["JWT_SECRET"] = "secret",
            ["JWT_ISSUER"] = "issuer",
            ["JWT_AUDIENCE"] = "audience",
        };

        var result = FlatEnvironmentVariables.Map(env.GetValueOrDefault);

        Assert.Equal("Host=db", result["ConnectionStrings:Database"]);
        Assert.Equal("redis:6379", result["ConnectionStrings:Redis"]);
        Assert.Equal("secret", result["Jwt:Secret"]);
        Assert.Equal("issuer", result["Jwt:Issuer"]);
        Assert.Equal("audience", result["Jwt:Audience"]);
    }

    [Fact]
    public void BlankVariablesDoNotOverrideOtherConfiguration()
    {
        var env = new Dictionary<string, string> { ["DATABASE_CONNECTION_STRING"] = "   " };

        var result = FlatEnvironmentVariables.Map(env.GetValueOrDefault);

        Assert.Empty(result);
    }

    [Fact]
    public void SplitsCorsOriginsIntoAnIndexedArray()
    {
        var env = new Dictionary<string, string> { ["CORS_ALLOWED_ORIGINS"] = "https://a.example, https://b.example,," };

        var result = FlatEnvironmentVariables.Map(env.GetValueOrDefault);

        Assert.Equal("https://a.example", result["Cors:AllowedOrigins:0"]);
        Assert.Equal("https://b.example", result["Cors:AllowedOrigins:1"]);
        Assert.False(result.ContainsKey("Cors:AllowedOrigins:2"));
    }
}
