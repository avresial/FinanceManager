using Blazored.LocalStorage;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Identity.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Shared.Services;

public class NavMenuStateCacheService(
    ILocalStorageService localStorageService,
    IMemoryCache memoryCache,
    TransactionLogHttpClient transactionLogHttpClient,
    AssetsHttpClient assetsHttpClient,
    LiabilitiesHttpClient liabilitiesHttpClient,
    ILogger<NavMenuStateCacheService> logger)
    : LocalStorageStateCacheService<NavMenuCacheSnapshot, UserSession, int>(localStorageService, memoryCache, logger, _cacheKeyPrefix)
{
    private const string _cacheKeyPrefix = "nav-menu-cache-v2";
    private static readonly TimeSpan _maxStale = TimeSpan.FromHours(24);

    public async Task<NavMenuCacheSnapshot?> GetCachedSnapshotAsync(int userId)
        => await GetCachedAsync(userId);

    protected override int GetCacheKey(UserSession refreshContext) => refreshContext.UserId;

    protected override async Task<NavMenuCacheSnapshot> BuildStateAsync(UserSession user)
    {
        var userId = user.UserId;
        var hasTransactionsTask = transactionLogHttpClient.GetLastTransactions(userId, 1);
        var displayAssetsTask = GetAssetsFlagAsync(userId);
        var displayLiabilitiesTask = GetLiabilitiesFlagAsync(userId);

        return new NavMenuCacheSnapshot
        {
            SchemaVersion = NavMenuCacheSnapshot.CurrentSchemaVersion,
            UserId = userId,
            FetchedAtUtc = DateTime.UtcNow,
            HasTransactions = (await hasTransactionsTask).Count > 0,
            DisplayAssetsLink = await displayAssetsTask,
            DisplayLiabilitiesLink = await displayLiabilitiesTask
        };
    }

    private async Task<bool> GetAssetsFlagAsync(int userId)
    {
        try
        {
            return await assetsHttpClient.IsAnyAccountWithAssets(userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while checking if any account has assets");
            return false;
        }
    }

    private async Task<bool> GetLiabilitiesFlagAsync(int userId)
    {
        try
        {
            return await liabilitiesHttpClient.IsAnyAccountWithLiabilities(userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while checking if any account has liabilities");
            return false;
        }
    }

    protected override bool IsUsable(NavMenuCacheSnapshot? snapshot, int cacheKey, DateTime utcNow)
    {
        if (snapshot is null)
            return false;

        if (snapshot.UserId != cacheKey)
            return false;

        if (snapshot.SchemaVersion != NavMenuCacheSnapshot.CurrentSchemaVersion)
            return false;

        return utcNow - snapshot.FetchedAtUtc <= _maxStale;
    }
}