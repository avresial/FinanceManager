using Blazored.LocalStorage;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Models;
using FinanceManager.Domain.Entities.Currencies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

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

    protected override string GetCacheKey(InvestmentPaycheckEstimateRefreshContext refreshContext)
        => BuildCacheKey(refreshContext.UserId, refreshContext.CurrencyId, refreshContext.EndDateTime, refreshContext.WithdrawalRate, refreshContext.SalaryMonths);

    protected override async Task<InvestmentPaycheckEstimateCacheSnapshot> BuildStateAsync(InvestmentPaycheckEstimateRefreshContext refreshContext)
    {
        var normalizedRate = Math.Round(refreshContext.WithdrawalRate, 4);
        var endDate = refreshContext.EndDateTime;

        var estimate = await assetsHttpClient.GetInvestmentPaycheckEstimate(
            refreshContext.UserId,
            DefaultCurrency.PLN,
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

    private static string BuildCacheKey(int userId, int currencyId, DateTime endDateTime, decimal withdrawalRate, int salaryMonths)
        => $"{userId}:{currencyId}:{endDateTime:O}:{Math.Round(withdrawalRate, 4):0.0000}:{salaryMonths}";
}