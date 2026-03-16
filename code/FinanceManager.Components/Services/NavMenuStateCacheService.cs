using Blazored.LocalStorage;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Models;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinanceManager.Components.Services;

public class NavMenuStateCacheService(
    ILocalStorageService localStorageService,
    IFinancialAccountService financialAccountService,
    AssetsHttpClient assetsHttpClient,
    LiabilitiesHttpClient liabilitiesHttpClient,
    ILogger<NavMenuStateCacheService> logger)
{
    private const string _cacheKeyPrefix = "nav-menu-cache-v1";
    private static readonly TimeSpan _maxStale = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Dictionary<int, NavMenuCacheSnapshot> _memoryCache = [];

    public async Task<NavMenuCacheSnapshot?> GetCachedSnapshotAsync(int userId)
    {
        if (_memoryCache.TryGetValue(userId, out var memorySnapshot) && IsUsable(memorySnapshot))
            return memorySnapshot;

        try
        {
            var persistedSnapshot = await localStorageService.GetItemAsync<NavMenuCacheSnapshot>(BuildCacheKey(userId));
            if (!IsUsable(persistedSnapshot))
            {
                await localStorageService.RemoveItemAsync(BuildCacheKey(userId));
                return null;
            }

            if (persistedSnapshot is null)
            {
                _memoryCache.Remove(userId);
                return null;
            }

            _memoryCache[userId] = persistedSnapshot;
            return persistedSnapshot;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid nav menu cache payload for user {UserId}", userId);
            await localStorageService.RemoveItemAsync(BuildCacheKey(userId));
            return null;
        }
    }

    public async Task<NavMenuCacheSnapshot> RefreshAsync(UserSession user)
    {
        await _refreshLock.WaitAsync();

        try
        {
            var snapshot = await BuildSnapshotAsync(user);
            _memoryCache[user.UserId] = snapshot;
            await localStorageService.SetItemAsync(BuildCacheKey(user.UserId), snapshot);
            return snapshot;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task InvalidateAsync(int userId)
    {
        _memoryCache.Remove(userId);
        await localStorageService.RemoveItemAsync(BuildCacheKey(userId));
    }

    private async Task<NavMenuCacheSnapshot> BuildSnapshotAsync(UserSession user)
    {
        var availableAccountsTask = financialAccountService.GetAvailableAccounts();
        var displayAssetsTask = GetAssetsFlagAsync(user.UserId);
        var displayLiabilitiesTask = GetLiabilitiesFlagAsync(user.UserId);

        var availableAccounts = await availableAccountsTask;
        var accounts = await BuildAccountsAsync(user.UserId, availableAccounts);

        return new NavMenuCacheSnapshot
        {
            SchemaVersion = NavMenuCacheSnapshot.CurrentSchemaVersion,
            UserId = user.UserId,
            FetchedAtUtc = DateTime.UtcNow,
            Accounts = accounts,
            DisplayAssetsLink = await displayAssetsTask,
            DisplayLiabilitiesLink = await displayLiabilitiesTask,
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
            logger.LogError("Account type {AccountType} can not be handled, account id {AccountId}", accountType.Name, accountId);
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

    private static string BuildCacheKey(int userId) => $"{_cacheKeyPrefix}:{userId}";

    private static bool IsUsable(NavMenuCacheSnapshot? snapshot)
    {
        if (snapshot is null)
            return false;

        if (snapshot.SchemaVersion != NavMenuCacheSnapshot.CurrentSchemaVersion)
            return false;

        return DateTime.UtcNow - snapshot.FetchedAtUtc <= _maxStale;
    }
}