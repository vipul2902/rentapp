using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RentApp.Application.Common.Caching;
using StackExchange.Redis;

namespace RentApp.Infrastructure.Caching;

/// <summary>
/// Redis-backed <see cref="IAppCache"/>. Never throws for cache problems: if Redis is down or slow the
/// call is skipped (and logged), and the caller falls back to PostgreSQL.
/// </summary>
internal sealed partial class RedisAppCache(IConnectionMultiplexer redis, ILogger<RedisAppCache> logger) : IAppCache, IOrganizationDataVersion
{
    private const string Prefix = "rentapp:";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
        where T : class
    {
        if (!redis.IsConnected)
        {
            return null;
        }

        try
        {
            var value = await redis.GetDatabase().StringGetAsync(Prefix + key);
            return value.HasValue ? JsonSerializer.Deserialize<T>(value.ToString(), Json) : null;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or JsonException)
        {
            LogCacheFailure(logger, "read", ex);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken)
        where T : class
    {
        if (!redis.IsConnected)
        {
            return;
        }

        try
        {
            await redis.GetDatabase().StringSetAsync(Prefix + key, JsonSerializer.Serialize(value, Json), timeToLive);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            LogCacheFailure(logger, "write", ex);
        }
    }

    public async Task<long?> OrganizationVersionAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!redis.IsConnected)
        {
            return null;
        }

        try
        {
            var value = await redis.GetDatabase().StringGetAsync(VersionKey(organizationId));
            return value.HasValue && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) ? version : 0;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            LogCacheFailure(logger, "version read", ex);
            return null;
        }
    }

    public async Task BumpAsync(IReadOnlyCollection<Guid> organizationIds)
    {
        if (organizationIds.Count == 0 || !redis.IsConnected)
        {
            return;
        }

        try
        {
            var database = redis.GetDatabase();
            await Task.WhenAll(organizationIds.Select(id => database.StringIncrementAsync(VersionKey(id))));
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // Cached figures may be up to CacheTimeToLive old until Redis recovers.
            LogCacheFailure(logger, "version bump", ex);
        }
    }

    private static string VersionKey(Guid organizationId) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}org:{organizationId:N}:version");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache {Operation} failed; continuing without the cache")]
    private static partial void LogCacheFailure(ILogger logger, string operation, Exception exception);
}

/// <summary>Marks an organization's cached data as out of date.</summary>
internal interface IOrganizationDataVersion
{
    Task BumpAsync(IReadOnlyCollection<Guid> organizationIds);
}
