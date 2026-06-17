using Blazored.LocalStorage;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Models;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.Identity.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class NavMenuStateCacheService(
    ILocalStorageService localStorageService,
    IMemoryCache memoryCache,
    IFinancialAccountService financialAccountService,
    AssetsHttpClient assetsHttpClient,
    LiabilitiesHttpClient liabilitiesHttpClient,
    ILogger<NavMenuStateCacheService> logger)
    : LocalStorageStateCacheService<NavMenuCacheSnapshot, UserSession, int>(localStorageService, memoryCache, logger, _cacheKeyPrefix)
{
    private const string _cacheKeyPrefix = "nav-menu-cache-v1";
    private static readonly TimeSpan _maxStale = TimeSpan.FromHours(24);

    public async Task<NavMenuCacheSnapshot?> GetCachedSnapshotAsync(int userId)
        => await GetCachedAsync(userId);

    protected override int GetCacheKey(UserSession refreshContext) => refreshContext.UserId;

    protected override async Task<NavMenuCacheSnapshot> BuildStateAsync(UserSession user)
    {
        var userId = user.UserId;
        var availableAccountsTask = financialAccountService.GetAvailableAccounts();
        var displayAssetsTask = GetAssetsFlagAsync(userId);
        var displayLiabilitiesTask = GetLiabilitiesFlagAsync(userId);

        var availableAccounts = await availableAccountsTask;
        var accounts = await BuildAccountsAsync(userId, availableAccounts);

        return new NavMenuCacheSnapshot
        {
            SchemaVersion = NavMenuCacheSnapshot.CurrentSchemaVersion,
            UserId = userId,
            FetchedAtUtc = DateTime.UtcNow,
            Accounts = accounts,
            DisplayAssetsLink = await displayAssetsTask,
            DisplayLiabilitiesLink = await displayLiabilitiesTask
        };
    }

    private async Task<List<NavMenuAccountCacheItem>> BuildAccountsAsync(int userId, Dictionary<int, Type> availableAccounts)
    {
        var now = DateTime.UtcNow;
        var accountTasks = availableAccounts.Select(account => BuildAccountAsync(userId, account.Key, account.Value, now));
        var accountItems = await Task.WhenAll(accountTasks);

        return accountItems
            .Where(account => account is not null)
            .Select(account => account!)
            .ToList();
    }

    private async Task<NavMenuAccountCacheItem?> BuildAccountAsync(int userId, int accountId, Type accountType, DateTime now)
    {
        string name;

        if (accountType == typeof(CurrencyAccount))
            name = await GetAccountNameAsync<CurrencyAccount>(userId, accountId, now);
        else if (accountType == typeof(StockAccount))
            name = await GetAccountNameAsync<StockAccount>(userId, accountId, now);
        else if (accountType == typeof(BondAccount))
            name = await GetAccountNameAsync<BondAccount>(userId, accountId, now);
        else
        {
            logger.LogError("Account type {AccountType} can not be handled for an account.", accountType.Name);
            return null;
        }

        return new NavMenuAccountCacheItem
        {
            AccountId = accountId,
            Name = name,
        };
    }

    private async Task<string> GetAccountNameAsync<T>(int userId, int accountId, DateTime now) where T : BasicAccountInformation
    {
        var account = await financialAccountService.GetAccount<T>(userId, accountId, now, now);
        return account?.Name ?? string.Empty;
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