namespace RentApp.Application.Common.Caching;

/// <summary>
/// A best-effort cache (Redis). PostgreSQL stays the source of truth: every method tolerates the cache being
/// down. Reads then return null, so callers compute from the database, and writes are skipped.
/// </summary>
public interface IAppCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
        where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken)
        where T : class;

    /// <summary>
    /// A number that changes whenever the organization's data changes (bumped after each committed write).
    /// Putting it in a cache key makes old entries unreachable at once. Null when the cache is unavailable.
    /// </summary>
    Task<long?> OrganizationVersionAsync(Guid organizationId, CancellationToken cancellationToken);
}
