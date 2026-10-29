using Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Caching;

// Entries are keyed by a per-user version: invalidation only moves the version, so old entries are
// never read again and simply expire. A read that started before a change stores under the old version
public sealed class UserReadCache : IUserReadCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);
    // Outlives every entry, so a forgotten version can't bring old entries back
    private static readonly TimeSpan VersionLifetime = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;
    private long _lastVersion;

    public UserReadCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrCreateAsync<T>(Guid userId, string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        var entryKey = $"user-read:{userId}:{CurrentVersion(userId)}:{key}";

        if (_cache.TryGetValue(entryKey, out T? cached) && cached is not null)
            return cached;

        var value = await factory();
        _cache.Set(entryKey, value, ttl ?? DefaultTtl);

        return value;
    }

    public void Invalidate(Guid userId)
    {
        _cache.Set(
            VersionKey(userId),
            Interlocked.Increment(ref _lastVersion),
            new MemoryCacheEntryOptions { SlidingExpiration = VersionLifetime });
    }

    private long CurrentVersion(Guid userId) =>
        _cache.TryGetValue(VersionKey(userId), out long version) ? version : 0;

    private static string VersionKey(Guid userId) => $"user-read-version:{userId}";
}
