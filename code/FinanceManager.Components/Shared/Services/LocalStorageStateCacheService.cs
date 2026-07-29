using Blazored.LocalStorage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinanceManager.Components.Shared.Services;

/// <summary>
/// Time-based data cache over memory + local storage: while a cached entry is still valid it is
/// returned and <b>no API request is made</b>.
/// </summary>
/// <remarks>
/// This is not the UI snapshot mechanism. Use <see cref="ISnapshotRefreshCoordinator"/> when the
/// goal is to paint the last-rendered state immediately and still always re-request fresh data —
/// see docs/codebase/UI-SNAPSHOTS.md for the comparison.
/// </remarks>
public abstract class LocalStorageStateCacheService<TState, TRefreshContext, TCacheKey>(
    ILocalStorageService localStorageService,
    IMemoryCache memoryCache,
    ILogger logger,
    string cacheKeyPrefix)
    where TState : class
    where TCacheKey : notnull
{
    private static readonly TimeSpan _memoryStateSlidingExpiration = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan _memoryStateAbsoluteExpiration = TimeSpan.FromHours(2);
    private static readonly TimeSpan _refreshLockSlidingExpiration = TimeSpan.FromMinutes(30);

    public async Task<TState?> GetCachedAsync(TCacheKey cacheKey)
    {
        var utcNow = DateTime.UtcNow;
        var memoryStateKey = BuildMemoryStateKey(cacheKey);

        if (memoryCache.TryGetValue(memoryStateKey, out TState? memorySnapshot) && IsUsable(memorySnapshot, cacheKey, utcNow))
            return memorySnapshot;

        memoryCache.Remove(memoryStateKey);

        try
        {
            var persistedSnapshot = await localStorageService.GetItemAsync<TState>(BuildStorageKey(cacheKey));
            if (!IsUsable(persistedSnapshot, cacheKey, utcNow))
            {
                memoryCache.Remove(memoryStateKey);
                await localStorageService.RemoveItemAsync(BuildStorageKey(cacheKey));
                return null;
            }

            if (persistedSnapshot is null)
            {
                memoryCache.Remove(memoryStateKey);
                return null;
            }

            memoryCache.Set(memoryStateKey, persistedSnapshot, BuildStateMemoryOptions());
            return persistedSnapshot;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid {CacheType} payload encountered during deserialization.", typeof(TState).Name);
            memoryCache.Remove(memoryStateKey);
            await localStorageService.RemoveItemAsync(BuildStorageKey(cacheKey));
            return null;
        }
    }

    public async Task<TState> GetOrRefreshAsync(TRefreshContext refreshContext)
    {
        var cacheKey = GetCacheKey(refreshContext);
        var cachedSnapshot = await GetCachedAsync(cacheKey);
        if (cachedSnapshot is not null)
            return cachedSnapshot;

        var refreshLock = GetRefreshLock(cacheKey);
        var memoryStateKey = BuildMemoryStateKey(cacheKey);

        await refreshLock.WaitAsync();

        try
        {
            cachedSnapshot = await GetCachedAsync(cacheKey);
            if (cachedSnapshot is not null)
                return cachedSnapshot;

            var snapshot = await BuildStateAsync(refreshContext);
            memoryCache.Set(memoryStateKey, snapshot, BuildStateMemoryOptions());
            await localStorageService.SetItemAsync(BuildStorageKey(cacheKey), snapshot);
            await PruneSupersededEntriesAsync(cacheKey);
            return snapshot;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public async Task<TState> RefreshAsync(TRefreshContext refreshContext)
    {
        var cacheKey = GetCacheKey(refreshContext);
        var refreshLock = GetRefreshLock(cacheKey);
        var memoryStateKey = BuildMemoryStateKey(cacheKey);

        await refreshLock.WaitAsync();

        try
        {
            var snapshot = await BuildStateAsync(refreshContext);
            memoryCache.Set(memoryStateKey, snapshot, BuildStateMemoryOptions());
            await localStorageService.SetItemAsync(BuildStorageKey(cacheKey), snapshot);
            await PruneSupersededEntriesAsync(cacheKey);
            return snapshot;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public async Task InvalidateAsync(TCacheKey cacheKey)
    {
        memoryCache.Remove(BuildMemoryStateKey(cacheKey));
        await localStorageService.RemoveItemAsync(BuildStorageKey(cacheKey));
    }

    protected abstract TCacheKey GetCacheKey(TRefreshContext refreshContext);

    protected abstract Task<TState> BuildStateAsync(TRefreshContext refreshContext);

    protected abstract bool IsUsable(TState? state, TCacheKey cacheKey, DateTime utcNow);

    /// <summary>
    /// When <see langword="true"/>, a successful write removes every other persisted entry sharing this
    /// cache's prefix, so the cache holds at most one entry at a time.
    /// </summary>
    /// <remarks>
    /// Opt in when the key covers only inputs the user does not switch between (one entry per user and
    /// currency): any key change is then a supersession — a schema bump, a day rollover — and the previous
    /// entry is dead weight that would otherwise sit in local storage forever. Leave it
    /// <see langword="false"/> for caches that legitimately hold several entries at once, such as one per
    /// date range the user can pick, where pruning would throw away entries still worth reading.
    /// </remarks>
    protected virtual bool RetainsOnlyLatestEntry => false;

    private async Task PruneSupersededEntriesAsync(TCacheKey cacheKey)
    {
        if (!RetainsOnlyLatestEntry)
            return;

        var currentStorageKey = BuildStorageKey(cacheKey);

        try
        {
            var supersededKeys = (await localStorageService.KeysAsync())
                .Where(key => key.StartsWith($"{cacheKeyPrefix}:", StringComparison.Ordinal)
                              && !string.Equals(key, currentStorageKey, StringComparison.Ordinal))
                .ToList();

            if (supersededKeys.Count == 0)
                return;

            await localStorageService.RemoveItemsAsync(supersededKeys);
        }
        catch (Exception ex)
        {
            // Pruning is housekeeping: the freshly written entry is already usable, so a failure here
            // must not surface to the caller.
            logger.LogWarning(ex, "Failed to prune superseded {CacheType} entries.", typeof(TState).Name);
        }
    }

    private SemaphoreSlim GetRefreshLock(TCacheKey cacheKey)
    {
        var refreshLockKey = BuildRefreshLockKey(cacheKey);
        return memoryCache.GetOrCreate(refreshLockKey, entry =>
        {
            entry.SlidingExpiration = _refreshLockSlidingExpiration;
            return new SemaphoreSlim(1, 1);
        }) ?? new SemaphoreSlim(1, 1);
    }

    private MemoryCacheEntryOptions BuildStateMemoryOptions()
        => new()
        {
            SlidingExpiration = _memoryStateSlidingExpiration,
            AbsoluteExpirationRelativeToNow = _memoryStateAbsoluteExpiration,
        };

    private string BuildMemoryStateKey(TCacheKey cacheKey) => $"{cacheKeyPrefix}:memory:{cacheKey}";

    private string BuildRefreshLockKey(TCacheKey cacheKey) => $"{cacheKeyPrefix}:refresh-lock:{cacheKey}";

    protected string BuildStorageKey(TCacheKey cacheKey) => $"{cacheKeyPrefix}:{cacheKey}";
}