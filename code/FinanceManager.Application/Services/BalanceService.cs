using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class BalanceService(IFinancialAccountRepository financialAccountRepository) : IBalanceService
{
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    public async Task<List<TimeSeriesModel>> GetIncome(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await AggregateByDay(userId, start, end, entry => entry.ValueChange > 0);
        return BucketToSeries(result);
    }

    public async Task<List<TimeSeriesModel>> GetSpending(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await AggregateByDay(userId, start, end, entry => entry.ValueChange < 0);
        return BucketToSeries(result);
    }

    public async Task<List<TimeSeriesModel>> GetBalance(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await AggregateByDay(userId, start, end, _ => true);
        return BucketToSeries(result);
    }

    private async Task<Dictionary<DateTime, decimal>> AggregateByDay(int userId, DateTime start, DateTime end, Func<FinancialEntryBase, bool> predicate)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;

            for (var date = end.Date; date >= start.Date; date = date.Add(-OneDay))
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

    private static List<TimeSeriesModel> BucketToSeries(Dictionary<DateTime, decimal> data) =>
        TimeBucketService.Get(data.Select(x => (x.Key, x.Value)))
                         .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Sum()))
                         .ToList();
}