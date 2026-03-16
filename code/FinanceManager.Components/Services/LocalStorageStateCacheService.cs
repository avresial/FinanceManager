using Blazored.LocalStorage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinanceManager.Components.Services;

public abstract class LocalStorageStateCacheService<TState, TRefreshContext, TCacheKey>(
    ILocalStorageService localStorageService,
    IMemoryCache memoryCache,
    ILogger logger,
    string cacheKeyPrefix) : IUiStateCacheService<TState, TRefreshContext, TCacheKey>
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
            logger.LogWarning(ex, "Invalid {CacheType} payload for key {CacheKey}", typeof(TState).Name, cacheKey);
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