using FinanceManager.Application.Dashboard;
using FinanceManager.Application.Shared;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Extensions;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Balance;

/// <remarks>
/// Without an account filter the balances come from the owner's pooled cash entries; an account filter keeps reading
/// the account's own entries for the account detail charts.
/// </remarks>
public class CurrencyBalanceService(
    ICashTransactionReader cashTransactionReader,
    IFinancialAccountRepository financialAccountRepository) : IBalanceServiceTyped
{
    private static readonly TimeSpan _oneDay = TimeSpan.FromDays(1);

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetInflow(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        var result = await AggregateByDay(userId, start, end, change => change > 0, accountIds);
        return BucketToSeries(result);
    }

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetOutflow(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        var result = await AggregateByDay(userId, start, end, change => change < 0, accountIds);
        return BucketToSeries(result);
    }

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetNetCashFlow(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        var result = await AggregateByDay(userId, start, end, _ => true, accountIds);
        return BucketToSeries(result);
    }

    // Capital is meaningful only for investment and bond account histories. Currency account
    // entries remain ordinary money-flow records and must not be counted on an account-value chart.
    public Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end) =>
        GetClosingBalance(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        var result = await AggregateClosingBalanceByDay(userId, start, end, accountIds);
        return BucketToClosingBalanceSeries(result);
    }

    private async Task<Dictionary<DateTime, decimal>> AggregateByDay(int userId, DateTime start, DateTime end, Func<decimal, bool> predicate, IReadOnlyCollection<int> accountIds)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);

        var result = InitializeDailyResult(start, end);
        if (accountIds.Count == 0)
        {
            foreach (var point in (await cashTransactionReader.GetValueRange(userId, start, end)).Points)
            {
                var day = point.PostingDate.Date;
                if (day < start.Date || day > end.Date || !predicate(point.ValueChange)) continue;

                result[day] += point.ValueChange;
            }

            return result;
        }

        var accountIdFilter = accountIds.ToHashSet();

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;
            if (!accountIdFilter.Contains(account.AccountId)) continue;

            foreach (var entry in account.Entries)
            {
                var day = entry.PostingDate.Date;
                if (day < start.Date || day > end.Date || !predicate(entry.ValueChange)) continue;

                result[day] += entry.ValueChange;
            }
        }

        return result;
    }

    private async Task<Dictionary<DateTime, decimal>> AggregateClosingBalanceByDay(int userId, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);

        var result = InitializeDailyResult(start, end);
        if (accountIds.Count == 0)
        {
            var walk = new OwnerLedgerWalk(await cashTransactionReader.GetValueRange(userId, start, end));
            foreach (var (day, positions) in walk.EachDayEnd(start, end))
                result[day] = positions.Sum(p => p.Value);

            return result;
        }

        var accountIdFilter = accountIds.ToHashSet();

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (!accountIdFilter.Contains(account.AccountId)) continue;

            var orderedEntries = account.Entries.OrderBy(entry => entry.PostingDate).ThenBy(entry => entry.EntryId).ToList();
            var runningBalance = account.NextOlderEntry?.Value ?? 0;
            var entryIndex = 0;

            while (entryIndex < orderedEntries.Count && orderedEntries[entryIndex].PostingDate.Date < start.Date)
            {
                runningBalance = orderedEntries[entryIndex].Value;
                entryIndex++;
            }

            for (var date = start.Date; date <= end.Date; date = date.Add(_oneDay))
            {
                while (entryIndex < orderedEntries.Count && orderedEntries[entryIndex].PostingDate.Date <= date)
                {
                    runningBalance = orderedEntries[entryIndex].Value;
                    entryIndex++;
                }

                result[date] += runningBalance;
            }
        }

        return result;
    }

    private static Dictionary<DateTime, decimal> InitializeDailyResult(DateTime start, DateTime end)
    {
        Dictionary<DateTime, decimal> result = [];

        for (var date = start.Date; date <= end.Date; date = date.Add(_oneDay))
            result[date] = 0;

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