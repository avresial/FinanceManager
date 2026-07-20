using Blazored.LocalStorage;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Models;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
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

        // Only the id crosses the wire, so the requested currency can be rebuilt from the context.
        var currency = new Currency { Id = refreshContext.CurrencyId };
        var assetsTimeSeriesTask = assetsHttpClient.GetAssetsTimeSeries(refreshContext.UserId, currency, startDate, endDate);
        var assetsPerTypeTask = assetsHttpClient.GetEndAssetsPerType(refreshContext.UserId, currency, endDate);
        var assetsPerAccountTask = assetsHttpClient.GetEndAssetsPerAccount(refreshContext.UserId, currency, endDate);
        var monthlyInvestmentRatesTask = GetMonthlyInvestmentRatesAsync(refreshContext.UserId, currency, endDate);

        await Task.WhenAll(assetsTimeSeriesTask, assetsPerTypeTask, assetsPerAccountTask, monthlyInvestmentRatesTask);

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
            MonthlyInvestmentRates = await monthlyInvestmentRatesTask,
        };
    }

    private async Task<List<InvestmentRate>> GetMonthlyInvestmentRatesAsync(int userId, Currency currency, DateTime endDate)
    {
        const int monthsBack = 12;
        var anchor = new DateTime(endDate.Year, endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var tasks = new Task<(DateTime start, DateTime end, List<InvestmentRate> rates)>[monthsBack];
        for (int i = 0; i < monthsBack; i++)
        {
            var monthStart = anchor.AddMonths(-(monthsBack - 1 - i));
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
            if (monthEnd > endDate) monthEnd = endDate;
            tasks[i] = FetchMonthAsync(userId, currency, monthStart, monthEnd);
        }

        var results = await Task.WhenAll(tasks);
        return [.. results.Select(r => r.rates.FirstOrDefault() ?? new InvestmentRate { Start = r.start, End = r.end })];
    }

    private async Task<(DateTime start, DateTime end, List<InvestmentRate> rates)> FetchMonthAsync(int userId, Currency currency, DateTime start, DateTime end)
    {
        var rates = await moneyFlowHttpClient.GetInvestmentRate(userId, currency, start, end).ToListAsync();
        return (start, end, rates);
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