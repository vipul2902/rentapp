using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace RentApp.IntegrationTests.Infrastructure;

/// <summary>
/// Throwaway PostgreSQL and Redis containers shared by every integration test in the collection.
/// Requires Docker. Images match docker-compose.yml so tests run against the same versions as local dev.
/// </summary>
public sealed class ContainersFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public RedisContainer Redis { get; } = new RedisBuilder("redis:7-alpine").Build();

    public Task InitializeAsync() => Task.WhenAll(Postgres.StartAsync(), Redis.StartAsync());

    public async Task DisposeAsync()
    {
        await Postgres.DisposeAsync();
        await Redis.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationTestGroup : ICollectionFixture<ContainersFixture>
{
    public const string Name = "Integration";
}
