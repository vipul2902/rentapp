using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using RentApp.Application.Common.Time;

namespace RentApp.IntegrationTests.Infrastructure;

/// <summary>Boots the real API (Testing environment) against the given database and Redis.</summary>
public sealed class RentAppFactory(
    string databaseConnectionString,
    string redisConnectionString,
    IReadOnlyDictionary<string, string>? overrides = null,
    DateTimeOffset? businessNow = null)
    : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-tests-only-secret-0123456789-abcdefghij";

    public static RentAppFactory For(ContainersFixture containers, IReadOnlyDictionary<string, string>? overrides = null) =>
        new(containers.Postgres.GetConnectionString(), containers.Redis.GetConnectionString(), overrides);

    /// <summary>Pins the business date (what "today" is for rent) without touching token or audit clocks.</summary>
    public static RentAppFactory At(ContainersFixture containers, DateTimeOffset businessNow) =>
        new(containers.Postgres.GetConnectionString(), containers.Redis.GetConnectionString(), businessNow: businessNow);

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

        // Tests drive rent generation explicitly instead of through the background worker.
        builder.UseSetting("RentGeneration:Enabled", "false");

        foreach (var (key, value) in overrides ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        if (businessNow is { } now)
        {
            builder.ConfigureTestServices(services => services.AddKeyedSingleton<TimeProvider>(BusinessTime.Key, new FakeTimeProvider(now)));
        }
    }
}
