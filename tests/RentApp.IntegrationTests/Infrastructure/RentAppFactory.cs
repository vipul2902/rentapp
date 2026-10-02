using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RentApp.IntegrationTests.Infrastructure;

/// <summary>Boots the real API (Testing environment) against the given database and Redis.</summary>
public sealed class RentAppFactory(
    string databaseConnectionString,
    string redisConnectionString,
    IReadOnlyDictionary<string, string>? overrides = null)
    : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-tests-only-secret-0123456789-abcdefghij";

    public static RentAppFactory For(ContainersFixture containers, IReadOnlyDictionary<string, string>? overrides = null) =>
        new(containers.Postgres.GetConnectionString(), containers.Redis.GetConnectionString(), overrides);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", databaseConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", redisConnectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Jwt:Issuer", "rentapp-tests");
        builder.UseSetting("Jwt:Audience", "rentapp-tests-client");

        // Tests register many accounts from one "IP"; the rate-limit test opts back in with a low limit.
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");

        foreach (var (key, value) in overrides ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
    }
}
