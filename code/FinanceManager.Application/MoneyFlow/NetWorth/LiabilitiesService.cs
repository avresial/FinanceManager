using FinanceManager.Application.Dashboard;
using FinanceManager.Application.Shared;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared;

namespace FinanceManager.Application.MoneyFlow.NetWorth;

/// <summary>
/// Liabilities are the pooled cash balances that are negative: all of a user's cash entries in one currency net against each
/// other, whatever account they belong to, and a currency whose net balance is below zero is a liability in that currency.
/// </summary>
public class LiabilitiesService(ICashTransactionReader cashTransactionReader, ICurrencyRepository currencyRepository) : ILiabilitiesService
{
    public async Task<bool> IsAnyAccountWithLiabilities(int userId) =>
        (await cashTransactionReader.GetPositions(userId, DateTime.UtcNow)).Any(p => p.Value < 0m);

    public async IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end)
    {
        var liabilities = (await cashTransactionReader.GetPositions(userId, end)).Where(p => p.Value < 0m).ToList();
        if (liabilities.Count == 0) yield break;

        var names = await GetCurrencyNames();
        foreach (var liability in liabilities)
            yield return new NameValueResult()
            {
                Name = names.GetValueOrDefault(liability.PartitionId) ?? $"Currency {liability.PartitionId}",
                Value = liability.Value,
                AccountId = null,
                // The balance is in its own currency, not converted to the display currency.
                CurrencyCode = names.GetValueOrDefault(liability.PartitionId),
            };
    }

    public async IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end)
    {
        var liabilities = (await cashTransactionReader.GetPositions(userId, end)).Where(p => p.Value < 0m).ToList();
        if (liabilities.Count == 0) yield break;

        // The total adds balances in their own currencies. When they all share one, the figure carries that code instead of
        // posing as the display currency; a mix stays unlabelled and the card notes that it is not converted.
        var currencyIds = liabilities.Select(p => p.PartitionId).Distinct().ToList();
        var code = currencyIds.Count == 1 ? (await GetCurrencyNames()).GetValueOrDefault(currencyIds[0]) : null;
        yield return new NameValueResult() { Name = "Cash", Value = liabilities.Sum(p => p.Value), AccountId = null, CurrencyCode = code };
    }

    public async IAsyncEnumerable<TimeSeriesModel> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end)
    {
        if (start == new DateTime()) yield break;
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);

        var walk = new OwnerLedgerWalk(await cashTransactionReader.GetValueRange(userId, start, end));
        Dictionary<DateTime, decimal> prices = [];
        foreach (var (day, positions) in walk.EachDayEnd(start, end))
            prices[day] = positions.Where(p => p.Value < 0m).Sum(p => p.Value);

        if (prices.Values.All(value => value == 0m)) yield break;

        var timeBucket = TimeBucketService.Get(prices.Select(x => (x.Key, x.Value))).OrderByDescending(x => x.Date);

        foreach (var price in timeBucket.Select(x => new TimeSeriesModel() { DateTime = x.Date, Value = x.Objects.Last() }))
            yield return price;
    }

    private async Task<Dictionary<int, string>> GetCurrencyNames() =>
        await currencyRepository.GetCurrencies().ToDictionaryAsync(currency => currency.Id, currency => currency.ShortName);
}