using Blazored.LocalStorage;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Models;
using FinanceManager.Domain.Entities.Currencies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class DashboardOverviewCardsCacheService(
    ILocalStorageService localStorageService,
    IMemoryCache memoryCache,
    MoneyFlowHttpClient moneyFlowHttpClient,
    ILogger<DashboardOverviewCardsCacheService> logger)
    : LocalStorageStateCacheService<DashboardOverviewCardsCacheSnapshot, DashboardOverviewCardsRefreshContext, string>(
        localStorageService,
        memoryCache,
        logger,
        _cacheKeyPrefix)
{
    private const string _cacheKeyPrefix = "dashboard-overview-cards-cache-v1";
    private static readonly TimeSpan _maxStale = TimeSpan.FromMinutes(5);

    public Task<DashboardOverviewCardsCacheSnapshot> GetSnapshotAsync(DashboardOverviewCardsRefreshContext context)
        => GetOrRefreshAsync(context);

    protected override string GetCacheKey(DashboardOverviewCardsRefreshContext refreshContext)
        => BuildCacheKey(refreshContext.UserId, refreshContext.CurrencyId, refreshContext.StartDateTime, refreshContext.EndDateTime);

    protected override async Task<DashboardOverviewCardsCacheSnapshot> BuildStateAsync(DashboardOverviewCardsRefreshContext refreshContext)
    {
        var startDate = refreshContext.StartDateTime.Date;
        var endDate = refreshContext.EndDateTime;

        var netCashFlowTask = moneyFlowHttpClient.GetNetCashFlow(refreshContext.UserId, DefaultCurrency.PLN, startDate, endDate);
        var closingBalanceTask = moneyFlowHttpClient.GetClosingBalance(refreshContext.UserId, DefaultCurrency.PLN, startDate, endDate);
        var netWorthTask = moneyFlowHttpClient.GetNetWorth(refreshContext.UserId, DefaultCurrency.PLN, endDate);

        await Task.WhenAll(netCashFlowTask, closingBalanceTask, netWorthTask);

        var snapshot = new DashboardOverviewCardsCacheSnapshot
        {
            SchemaVersion = DashboardOverviewCardsCacheSnapshot.CurrentSchemaVersion,
            UserId = refreshContext.UserId,
            CurrencyId = refreshContext.CurrencyId,
            StartDateTime = startDate,
            EndDateTime = endDate,
            FetchedAtUtc = DateTime.UtcNow,
            NetCashFlowSeries = [.. (await netCashFlowTask)],
            ClosingBalanceSeries = [.. (await closingBalanceTask)],
            NetWorth = await netWorthTask,
        };

        return snapshot;
    }

    protected override bool IsUsable(DashboardOverviewCardsCacheSnapshot? state, string cacheKey, DateTime utcNow)
    {
        if (state is null)
            return false;

        if (state.SchemaVersion != DashboardOverviewCardsCacheSnapshot.CurrentSchemaVersion)
            return false;

        if (utcNow - state.FetchedAtUtc > _maxStale)
            return false;

        return BuildCacheKey(state.UserId, state.CurrencyId, state.StartDateTime, state.EndDateTime) == cacheKey;
    }

    private static string BuildCacheKey(int userId, int currencyId, DateTime startDateTime, DateTime endDateTime)
        => $"{userId}:{currencyId}:{startDateTime.Date:O}:{endDateTime:O}";
}