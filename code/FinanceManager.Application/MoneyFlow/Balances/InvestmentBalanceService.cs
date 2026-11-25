using FinanceManager.Application.Dashboard;
using FinanceManager.Application.Shared;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Portfolio.Valuation.Services;

namespace FinanceManager.Application.MoneyFlow.Balances;

/// <summary>Values the owner's pooled holdings and remaining cost basis. Cash flows are recorded in the cash ledger.</summary>
internal class InvestmentBalanceService(
    IInvestmentValuationService investmentValuationService,
    IInvestmentCapitalSeriesService capitalSeriesService) : IBalanceServiceTyped
{
    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public async Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (userId == 0 || start == default || end == default || end.Date < start.Date) return [];

        var series = await capitalSeriesService.GetCapitalSeriesAsync(userId, currency, start.Date, end.Date);
        if (series.Count == 0) return [];

        Dictionary<DateTime, decimal> values = [];
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            values[date] = series.GetValueOrDefault(date);

        return ToBuckets(values);
    }

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);
        if (userId == 0 || end.Date < start.Date) return [];

        // The owner's pooled holdings are valued as one portfolio, so the series does not depend on which account a trade was booked in.
        var series = await investmentValuationService.GetUserValueSeriesAsync(userId, currency, start.Date, end.Date);
        if (series.Count == 0) return [];

        // Carry every day in the window so the bucketing picks the right closing point even on days the
        // valuation service omitted (a day that values to zero is left out of its series).
        Dictionary<DateTime, decimal> values = [];
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            values[date] = series.GetValueOrDefault(date);

        return ToBuckets(values);
    }

    private static List<TimeSeriesModel> ToBuckets(Dictionary<DateTime, decimal> values) =>
        TimeBucketService.Get(values.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                         .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                         .ToList();
}