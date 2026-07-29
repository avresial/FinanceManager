using Blazored.LocalStorage;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Services;

public class InvestmentRateCacheService(
    ILocalStorageService localStorageService,
    IMemoryCache memoryCache,
    MoneyFlowHttpClient moneyFlowHttpClient,
    ILogger<InvestmentRateCacheService> logger)
    : LocalStorageStateCacheService<InvestmentRateCacheSnapshot, InvestmentRateRefreshContext, string>(
        localStorageService,
        memoryCache,
        logger,
        _cacheKeyPrefix)
{
    private const string _cacheKeyPrefix = "investment-rate-cache-v1";
    private const int _monthsBack = 12;
    private static readonly TimeSpan _maxStale = TimeSpan.FromMinutes(5);

    public Task<InvestmentRateCacheSnapshot> GetSnapshotAsync(InvestmentRateRefreshContext context)
        => GetOrRefreshAsync(context);

    // The key covers one user and currency, so a key change always supersedes the previous entry.
    protected override bool RetainsOnlyLatestEntry => true;

    protected override string GetCacheKey(InvestmentRateRefreshContext refreshContext)
        => BuildCacheKey(refreshContext.UserId, refreshContext.CurrencyId, refreshContext.EndDateTime);

    protected override async Task<InvestmentRateCacheSnapshot> BuildStateAsync(InvestmentRateRefreshContext refreshContext)
    {
        var endDate = refreshContext.EndDateTime;
        var currency = new Currency { Id = refreshContext.CurrencyId };
        var monthlyInvestmentRates = await GetMonthlyInvestmentRatesAsync(refreshContext.UserId, currency, endDate);

        return new InvestmentRateCacheSnapshot
        {
            SchemaVersion = InvestmentRateCacheSnapshot.CurrentSchemaVersion,
            UserId = refreshContext.UserId,
            CurrencyId = refreshContext.CurrencyId,
            EndDateTime = endDate,
            FetchedAtUtc = DateTime.UtcNow,
            MonthlyInvestmentRates = monthlyInvestmentRates,
        };
    }

    private async Task<List<InvestmentRate>> GetMonthlyInvestmentRatesAsync(int userId, Currency currency, DateTime endDate)
    {
        var anchor = new DateTime(endDate.Year, endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var tasks = new Task<(DateTime start, DateTime end, List<InvestmentRate> rates)>[_monthsBack];
        for (int i = 0; i < _monthsBack; i++)
        {
            var monthStart = anchor.AddMonths(-(_monthsBack - 1 - i));
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

    protected override bool IsUsable(InvestmentRateCacheSnapshot? state, string cacheKey, DateTime utcNow)
    {
        if (state is null)
            return false;

        if (state.SchemaVersion != InvestmentRateCacheSnapshot.CurrentSchemaVersion)
            return false;

        if (utcNow - state.FetchedAtUtc > _maxStale)
            return false;

        return BuildCacheKey(state.UserId, state.CurrencyId, state.EndDateTime) == cacheKey;
    }

    // The card's "as of" instant is a live clock read, so it differs on every mount; only its day reaches
    // the key (see CacheKeyDate). Rates are reported per month, so entries built at different times of the
    // same day are equivalent — the exact instant stays on the snapshot for the request itself, and
    // staleness inside the day is bounded by the FetchedAtUtc check in IsUsable.
    private static string BuildCacheKey(int userId, int currencyId, DateTime endDateTime)
        => $"{userId}:{currencyId}:{CacheKeyDate.ToSegment(endDateTime)}";
}