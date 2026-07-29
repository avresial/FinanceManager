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

public class InvestmentPaycheckEstimateCacheService(
    ILocalStorageService localStorageService,
    IMemoryCache memoryCache,
    AssetsHttpClient assetsHttpClient,
    ILogger<InvestmentPaycheckEstimateCacheService> logger)
    : LocalStorageStateCacheService<InvestmentPaycheckEstimateCacheSnapshot, InvestmentPaycheckEstimateRefreshContext, string>(
        localStorageService,
        memoryCache,
        logger,
        _cacheKeyPrefix)
{
    private const string _cacheKeyPrefix = "investment-paycheck-estimate-cache-v1";
    private static readonly TimeSpan _maxStale = TimeSpan.FromMinutes(5);

    public async Task<InvestmentPaycheckEstimateCacheSnapshot> GetSnapshotAsync(InvestmentPaycheckEstimateRefreshContext context)
        => await GetOrRefreshAsync(context);

    // The key covers one user and currency, so a key change always supersedes the previous entry.
    protected override bool RetainsOnlyLatestEntry => true;

    protected override string GetCacheKey(InvestmentPaycheckEstimateRefreshContext refreshContext)
        => BuildCacheKey(refreshContext.UserId, refreshContext.CurrencyId, refreshContext.EndDateTime, refreshContext.WithdrawalRate, refreshContext.SalaryMonths);

    protected override async Task<InvestmentPaycheckEstimateCacheSnapshot> BuildStateAsync(InvestmentPaycheckEstimateRefreshContext refreshContext)
    {
        var normalizedRate = Math.Round(refreshContext.WithdrawalRate, 4);
        var endDate = refreshContext.EndDateTime;

        var currency = new Currency(refreshContext.CurrencyId, string.Empty, string.Empty);

        var estimate = await assetsHttpClient.GetInvestmentPaycheckEstimate(
            refreshContext.UserId,
            currency,
            endDate,
            normalizedRate,
            refreshContext.SalaryMonths);

        return new InvestmentPaycheckEstimateCacheSnapshot
        {
            SchemaVersion = InvestmentPaycheckEstimateCacheSnapshot.CurrentSchemaVersion,
            UserId = refreshContext.UserId,
            CurrencyId = refreshContext.CurrencyId,
            EndDateTime = endDate,
            WithdrawalRate = normalizedRate,
            SalaryMonths = refreshContext.SalaryMonths,
            FetchedAtUtc = DateTime.UtcNow,
            Estimate = estimate,
        };
    }

    protected override bool IsUsable(InvestmentPaycheckEstimateCacheSnapshot? state, string cacheKey, DateTime utcNow)
    {
        if (state is null)
            return false;

        if (state.SchemaVersion != InvestmentPaycheckEstimateCacheSnapshot.CurrentSchemaVersion)
            return false;

        if (utcNow - state.FetchedAtUtc > _maxStale)
            return false;

        return BuildCacheKey(state.UserId, state.CurrencyId, state.EndDateTime, state.WithdrawalRate, state.SalaryMonths) == cacheKey;
    }

    // The card's "as of" instant is a live clock read, so it differs on every mount; only its day reaches
    // the key (see CacheKeyDate). The exact instant stays on the snapshot for the request itself, and
    // staleness inside the day is bounded by the FetchedAtUtc check in IsUsable.
    private static string BuildCacheKey(int userId, int currencyId, DateTime endDateTime, decimal withdrawalRate, int salaryMonths)
        => $"{userId}:{currencyId}:{CacheKeyDate.ToSegment(endDateTime)}:{Math.Round(withdrawalRate, 4):0.0000}:{salaryMonths}";
}