using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services.Currencies;

public class CurrencyBalanceService(IFinancialAccountRepository financialAccountRepository) : IBalanceServiceTyped
{
    private static readonly TimeSpan _oneDay = TimeSpan.FromDays(1);

    public bool IsOfType<T>() => typeof(T) == typeof(CurrencyAccount);

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetInflow(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        var result = await AggregateByDay(userId, start, end, entry => entry.ValueChange > 0, accountIds);
        return BucketToSeries(result);
    }

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetOutflow(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        var result = await AggregateByDay(userId, start, end, entry => entry.ValueChange < 0, accountIds);
        return BucketToSeries(result);
    }

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetNetCashFlow(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        var result = await AggregateByDay(userId, start, end, _ => true, accountIds);
        return BucketToSeries(result);
    }

    public Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end) =>
        GetClosingBalance(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        var result = await AggregateClosingBalanceByDay(userId, start, end, accountIds);
        return BucketToClosingBalanceSeries(result);
    }

    private async Task<Dictionary<DateTime, decimal>> AggregateByDay(int userId, DateTime start, DateTime end, Func<FinancialEntryBase, bool> predicate, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];
        var accountIdFilter = accountIds.Count > 0 ? accountIds.ToHashSet() : [];

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;

            for (var date = end.Date; date >= start.Date; date = date.Add(-_oneDay))
            {
                if (!result.ContainsKey(date)) result.Add(date, 0);

                var entries = account.Get(date);
                foreach (var entry in entries.Select(x => x as FinancialEntryBase).Where(x => x is not null))
                {
                    if (entry!.PostingDate.Date != date.Date) continue;
                    if (!predicate(entry)) continue;
                    result[date] += entry.ValueChange;
                }
            }
        }

        return result;
    }

    private async Task<Dictionary<DateTime, decimal>> AggregateClosingBalanceByDay(int userId, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];
        var accountIdFilter = accountIds.Count > 0 ? accountIds.ToHashSet() : [];

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;

            for (var date = end.Date; date >= start.Date; date = date.Add(-_oneDay))
            {
                if (!result.ContainsKey(date)) result.Add(date, 0);

                var entry = account.GetThisOrNextOlder(date);
                if (entry is null) continue;

                result[date] += entry.Value;
            }
        }

        return result;
    }

    private static List<TimeSeriesModel> BucketToSeries(Dictionary<DateTime, decimal> data) =>
        TimeBucketService.Get(data.Select(x => (x.Key, x.Value)))
                         .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Sum()))
                         .ToList();

    private static List<TimeSeriesModel> BucketToClosingBalanceSeries(Dictionary<DateTime, decimal> data) =>
        TimeBucketService.Get(data.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                         .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                         .ToList();
}