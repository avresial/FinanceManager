using Blazored.LocalStorage;
using FinanceManager.Components.Models;
using FinanceManager.Domain.Dashboard.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinanceManager.Components.Services;

public class DashboardOverviewCacheService(
    ILocalStorageService _localStorageService,
    IMemoryCache _memoryCache,
    ILogger<DashboardOverviewCacheService> _logger)
{
    private const string _cacheKeyPrefix = "dashboard-overview-cache-v1";
    private static readonly TimeSpan _maxStale = TimeSpan.FromHours(24);
    private static readonly TimeSpan _memorySlidingExpiration = TimeSpan.FromMinutes(30);

    public async Task<DashboardOverviewCacheSnapshot?> GetCachedAsync(int userId, int currencyId, DateTime start, DateTime end)
    {
        var key = BuildKey(userId, currencyId, start, end);

        if (_memoryCache.TryGetValue(BuildMemoryKey(key), out DashboardOverviewCacheSnapshot? snapshot))
        {
            if (IsUsable(snapshot))
                return snapshot;
            _memoryCache.Remove(BuildMemoryKey(key));
        }

        try
        {
            snapshot = await _localStorageService.GetItemAsync<DashboardOverviewCacheSnapshot>(BuildStorageKey(key));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupt dashboard overview cache payload; evicting.");
            await _localStorageService.RemoveItemAsync(BuildStorageKey(key));
            return null;
        }

        if (!IsUsable(snapshot))
        {
            _memoryCache.Remove(BuildMemoryKey(key));
            await _localStorageService.RemoveItemAsync(BuildStorageKey(key));
            return null;
        }

        _memoryCache.Set(BuildMemoryKey(key), snapshot, _memorySlidingExpiration);
        return snapshot;
    }

    public async Task SaveAsync(DashboardOverviewDto overview)
    {
        var key = BuildKey(overview.UserId, overview.CurrencyId, overview.StartDate, overview.EndDate);
        var snapshot = DashboardOverviewCacheSnapshot.FromDto(overview);
        _memoryCache.Set(BuildMemoryKey(key), snapshot, _memorySlidingExpiration);
        await _localStorageService.SetItemAsync(BuildStorageKey(key), snapshot);
    }

    private static bool IsUsable(DashboardOverviewCacheSnapshot? snapshot)
        => snapshot is not null
           && snapshot.SchemaVersion == DashboardOverviewCacheSnapshot.CurrentSchemaVersion
           && DateTime.UtcNow - snapshot.FetchedAtUtc <= _maxStale;

    private static string BuildKey(int userId, int currencyId, DateTime start, DateTime end)
        => $"{userId}:{currencyId}:{start.Date:yyyy-MM-dd}:{end.Date:yyyy-MM-dd}";

    private static string BuildMemoryKey(string key) => $"{_cacheKeyPrefix}:memory:{key}";

    private static string BuildStorageKey(string key) => $"{_cacheKeyPrefix}:{key}";
}