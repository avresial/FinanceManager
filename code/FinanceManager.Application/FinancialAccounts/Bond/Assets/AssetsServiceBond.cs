using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Extensions;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Bond.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Application.FinancialAccounts.Bond.Assets;

public class AssetsServiceBond(
    IFinancialAccountRepository financialAccountRepository,
    IBondDetailsRepository bondDetailsRepository,
    IBondUnrealizedGainLossCalculator bondUnrealizedGainLossCalculator) : IAssetsServiceTyped
{
    public bool IsOfType<T>() => typeof(T) == typeof(BondAccount);
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = TimeSpan.FromDays(1);
        List<BondDetails> bondDetails = await bondDetailsRepository.GetAllAsync().ToListAsync();
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end).Where(x => x.ContainsAssets))
        {
            foreach (var price in account.GetDailyPrice(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end), bondDetails))
            {
                if (!prices.ContainsKey(price.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)))
                    prices.Add(price.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), price.Value);
                else
                    prices[price.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)] += price.Value;
            }
        }

        return prices.Select(x => new TimeSeriesModel(x.Key, x.Value)).ToList();
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = TimeSpan.FromDays(1);
        List<BondDetails> bondDetails = await bondDetailsRepository.GetAllAsync().ToListAsync();
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end).Where(x => x.ContainsAssets && x.AccountType.ToString() == investmentType.ToString()))
        {
            foreach (var price in account.GetDailyPrice(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end), bondDetails))
            {
                if (!prices.ContainsKey(price.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)))
                    prices.Add(price.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), price.Value);
                else
                    prices[price.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)] += price.Value;
            }
        }

        return prices.Select(x => new TimeSeriesModel(x.Key, x.Value)).ToList();
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        var bondDetails = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
        {
            decimal value = 0;
            foreach (var bondDetailsId in account.GetStoredBondsIds())
            {
                var latestEntry = account.GetThisOrNextOlder(asOfDate, bondDetailsId);
                if (latestEntry is null) continue;
                if (!bondDetails.TryGetValue(bondDetailsId, out var details))
                    throw new InvalidOperationException($"Bond valuation requires details for bond id {bondDetailsId}.");

                value += latestEntry.GetPriceAt(DateOnly.FromDateTime(asOfDate), details);
            }

            yield return new(account.Name, value);
        }
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        var bondDetails = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, asOfDate.AddMinutes(-1), asOfDate).Where(x => x.ContainsAssets))
        {
            decimal value = 0;
            foreach (var bondDetailsId in account.GetStoredBondsIds())
            {
                var latestEntry = account.GetThisOrNextOlder(asOfDate, bondDetailsId);
                if (latestEntry is null) continue;
                if (!bondDetails.TryGetValue(bondDetailsId, out var details))
                    throw new InvalidOperationException($"Bond valuation requires details for bond id {bondDetailsId}.");

                value += latestEntry.GetPriceAt(DateOnly.FromDateTime(asOfDate), details);
            }

            yield return new(account.Name, value);
        }
    }

    public Task<bool> IsAnyAccountWithAssets(int userId)
    {
        var end = DateTime.UtcNow;

        return financialAccountRepository.GetAccounts<BondAccount>(userId, end.AddDays(-1), end).AnyAsync(x => x.ContainsAssets).AsTask();
    }

    public async Task<List<UnrealizedGainLossAccountResult>> GetUnrealizedGainLossPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        var instrumentResults = await GetUnrealizedGainLossPerInstrument(userId, currency, asOfDate);
        var byAccount = instrumentResults.GroupBy(x => new { x.AccountId, x.AccountName });

        List<UnrealizedGainLossAccountResult> results = [];
        foreach (var accountGroup in byAccount)
        {
            var included = accountGroup.Where(x => !x.IsExcludedFromTotals).ToList();
            var excludedCount = accountGroup.Count(x => x.IsExcludedFromTotals);

            var costBasis = included.Sum(x => x.CostBasis);
            var currentValue = included.Sum(x => x.CurrentValue);
            var unrealized = currentValue - costBasis;
            var unrealizedPercent = costBasis == 0 ? 0 : unrealized / costBasis * 100;

            results.Add(new UnrealizedGainLossAccountResult(
                accountGroup.Key.AccountId,
                accountGroup.Key.AccountName,
                costBasis,
                currentValue,
                unrealized,
                unrealizedPercent,
                asOfDate,
                excludedCount
            ));
        }

        return results;
    }

    public async Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossPerInstrument(int userId, Currency currency, DateTime asOfDate)
    {
        var bondDetailsById = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);
        List<UnrealizedGainLossInstrumentResult> results = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, DateTime.MinValue, asOfDate).Where(x => x.ContainsAssets))
        {
            foreach (var bondDetailsId in account.GetStoredBondsIds())
            {
                bondDetailsById.TryGetValue(bondDetailsId, out var details);

                var instrumentResult = await bondUnrealizedGainLossCalculator.CalculateAsync(account, bondDetailsId, details, currency, asOfDate);
                if (instrumentResult is not null)
                    results.Add(instrumentResult);
            }
        }

        return results;
    }
}