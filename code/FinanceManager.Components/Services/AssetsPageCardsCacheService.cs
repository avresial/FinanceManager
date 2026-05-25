using Blazored.LocalStorage;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Models;
using FinanceManager.Domain.Entities.Currencies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class AssetsPageCardsCacheService(
    ILocalStorageService localStorageService,
    IMemoryCache memoryCache,
    AssetsHttpClient assetsHttpClient,
    MoneyFlowHttpClient moneyFlowHttpClient,
    ILogger<AssetsPageCardsCacheService> logger)
    : LocalStorageStateCacheService<AssetsPageCardsCacheSnapshot, AssetsPageCardsRefreshContext, string>(
        localStorageService,
        memoryCache,
        logger,
        _cacheKeyPrefix)
{
    private const string _cacheKeyPrefix = "assets-page-cards-cache-v1";
    private static readonly TimeSpan _maxStale = TimeSpan.FromMinutes(5);

    public Task<AssetsPageCardsCacheSnapshot> GetSnapshotAsync(AssetsPageCardsRefreshContext context)
        => GetOrRefreshAsync(context);

    protected override string GetCacheKey(AssetsPageCardsRefreshContext refreshContext)
        => BuildCacheKey(refreshContext.UserId, refreshContext.CurrencyId, refreshContext.StartDateTime, refreshContext.EndDateTime);

    protected override async Task<AssetsPageCardsCacheSnapshot> BuildStateAsync(AssetsPageCardsRefreshContext refreshContext)
    {
        var startDate = refreshContext.StartDateTime.Date;
        var endDate = refreshContext.EndDateTime;

        var assetsTimeSeriesTask = assetsHttpClient.GetAssetsTimeSeries(refreshContext.UserId, DefaultCurrency.PLN, startDate, endDate);
        var assetsPerTypeTask = assetsHttpClient.GetEndAssetsPerType(refreshContext.UserId, DefaultCurrency.PLN, endDate);
        var assetsPerAccountTask = assetsHttpClient.GetEndAssetsPerAccount(refreshContext.UserId, DefaultCurrency.PLN, endDate);
        var investmentRateTask = moneyFlowHttpClient.GetInvestmentRate(refreshContext.UserId, startDate, endDate).ToListAsync().AsTask();

        await Task.WhenAll(assetsTimeSeriesTask, assetsPerTypeTask, assetsPerAccountTask, investmentRateTask);

        return new AssetsPageCardsCacheSnapshot
        {
            SchemaVersion = AssetsPageCardsCacheSnapshot.CurrentSchemaVersion,
            UserId = refreshContext.UserId,
            CurrencyId = refreshContext.CurrencyId,
            StartDateTime = startDate,
            EndDateTime = endDate,
            FetchedAtUtc = DateTime.UtcNow,
            AssetsTimeSeries = [.. (await assetsTimeSeriesTask)],
            EndAssetsPerType = [.. (await assetsPerTypeTask)],
            EndAssetsPerAccount = [.. (await assetsPerAccountTask)],
            InvestmentRates = [.. (await investmentRateTask)],
        };
    }

    protected override bool IsUsable(AssetsPageCardsCacheSnapshot? state, string cacheKey, DateTime utcNow)
    {
        if (state is null)
            return false;

        if (state.SchemaVersion != AssetsPageCardsCacheSnapshot.CurrentSchemaVersion)
            return false;

        if (utcNow - state.FetchedAtUtc > _maxStale)
            return false;

        return BuildCacheKey(state.UserId, state.CurrencyId, state.StartDateTime, state.EndDateTime) == cacheKey;
    }

    private static string BuildCacheKey(int userId, int currencyId, DateTime startDateTime, DateTime endDateTime)
        => $"{userId}:{currencyId}:{startDateTime.Date:O}:{endDateTime:O}";
}