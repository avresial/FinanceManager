using FinanceManager.Application.Dashboard;
using FinanceManager.Application.Shared;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Balance;

/// <remarks>Balances combine the owner's signed changes per currency.</remarks>
public class CurrencyBalanceService(
    ICashTransactionReader cashTransactionReader) : IBalanceServiceTyped
{
    private static readonly TimeSpan _oneDay = TimeSpan.FromDays(1);

    public async Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await AggregateByDay(userId, start, end, change => change > 0);
        return BucketToSeries(result);
    }

    public async Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await AggregateByDay(userId, start, end, change => change < 0);
        return BucketToSeries(result);
    }

    public async Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await AggregateByDay(userId, start, end, _ => true);
        return BucketToSeries(result);
    }

    // Capital is meaningful only for investment and bond account histories. Currency account
    // entries remain ordinary money-flow records and must not be counted on an account-value chart.
    public Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await AggregateClosingBalanceByDay(userId, start, end);
        return BucketToClosingBalanceSeries(result);
    }

    private async Task<Dictionary<DateTime, decimal>> AggregateByDay(int userId, DateTime start, DateTime end, Func<decimal, bool> predicate)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);

        var result = InitializeDailyResult(start, end);
        foreach (var point in (await cashTransactionReader.GetValueRange(userId, start, end)).Points)
        {
            var day = point.PostingDate.Date;
            if (day < start.Date || day > end.Date || !predicate(point.ValueChange)) continue;

            result[day] += point.ValueChange;
        }

        return result;
    }

    private async Task<Dictionary<DateTime, decimal>> AggregateClosingBalanceByDay(int userId, DateTime start, DateTime end)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);

        var result = InitializeDailyResult(start, end);
        var walk = new OwnerLedgerWalk(await cashTransactionReader.GetValueRange(userId, start, end));
        foreach (var (day, positions) in walk.EachDayEnd(start, end))
            result[day] = positions.Sum(p => p.Value);

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