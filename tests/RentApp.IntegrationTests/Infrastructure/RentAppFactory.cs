using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RentApp.IntegrationTests.Infrastructure;

/// <summary>Boots the real API (Testing environment) against the given database and Redis.</summary>
public sealed class RentAppFactory(string databaseConnectionString, string redisConnectionString)
    : WebApplicationFactory<Program>
{
    public static RentAppFactory For(ContainersFixture containers) =>
        new(containers.Postgres.GetConnectionString(), containers.Redis.GetConnectionString());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", databaseConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", redisConnectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
    }
}
