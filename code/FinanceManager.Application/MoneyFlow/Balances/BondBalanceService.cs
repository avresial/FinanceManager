using FinanceManager.Application.Currencies.ExchangeRates;
using FinanceManager.Application.Dashboard;
using FinanceManager.Application.Shared;
using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Shared;

namespace FinanceManager.Application.MoneyFlow.Balances;

/// <remarks>Values bond units per owner and definition from the latest pooled entry.</remarks>
internal class BondBalanceService(
    IBondTransactionReader bondTransactionReader,
    BondDashboardContext bondDashboardContext,
    ICurrencyExchangeService currencyExchangeService) : IBalanceServiceTyped
{
    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        AggregateByDay(userId, start, end, change => change > 0);

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        AggregateByDay(userId, start, end, change => change < 0);

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end) =>
        AggregateByDay(userId, start, end, _ => true);

    public async Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == default || end == default || end.Date < start.Date) return [];
        return await GetOwnerCapital(userId, currency, start, end);
    }

    // Capital is what the user put in: the units already held before the range count on its first day, then each entry's unit movement.
    // Interest changes the pooled value through bond pricing and never changes contributed capital.
    private async Task<List<TimeSeriesModel>> GetOwnerCapital(int userId, Currency currency, DateTime start, DateTime end)
    {
        var range = await bondTransactionReader.GetValueRange(userId, start, end);
        // A bond bought and fully sold before the range has a zero-unit opening: it neither needs its definition nor contributes capital.
        var opening = range.Opening.Where(p => p.Value != 0m).ToList();
        var bondDetails = await bondDashboardContext.LoadReferencedDetailsAsync(
            opening.Select(p => p.PartitionId).Concat(range.Points.Select(p => p.PartitionId)));

        BondDetails GetDetails(int bondDetailsId) => bondDetails.TryGetValue(bondDetailsId, out var details)
            ? details
            : throw new InvalidOperationException($"Bond capital requires details for bond id {bondDetailsId}.");

        List<BondCapitalFlow> flows =
        [
            .. opening.Select(p => new BondCapitalFlow(start.Date, p.Value * GetDetails(p.PartitionId).UnitValue, GetDetails(p.PartitionId).Currency)),
            .. range.Points.Select(p => new BondCapitalFlow(p.PostingDate.Date, p.ValueChange * GetDetails(p.PartitionId).UnitValue, GetDetails(p.PartitionId).Currency)),
        ];

        return await BucketCapital(flows, currency, start, end);
    }

    private async Task<List<TimeSeriesModel>> BucketCapital(List<BondCapitalFlow> flows, Currency currency, DateTime start, DateTime end)
    {
        if (flows.Count == 0) return [];

        var ratesByCurrency = new Dictionary<string, IReadOnlyDictionary<DateTime, decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in flows.GroupBy(flow => flow.Currency.ShortName, StringComparer.OrdinalIgnoreCase))
        {
            ratesByCurrency[group.Key] = await CurrencyRateSeries.LoadAsync(
                currencyExchangeService,
                group.First().Currency,
                currency,
                start.Date,
                end.Date);
        }

        Dictionary<DateTime, decimal> dailyDeltas = [];
        foreach (var flow in flows)
        {
            if (!ratesByCurrency.TryGetValue(flow.Currency.ShortName, out var rates)
                || !CurrencyRateSeries.TryGet(rates, flow.Date, out var rate))
                continue;

            dailyDeltas[flow.Date] = dailyDeltas.GetValueOrDefault(flow.Date) + flow.Amount * rate;
        }

        Dictionary<DateTime, decimal> cumulative = [];
        decimal capital = 0m;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            capital += dailyDeltas.GetValueOrDefault(date);
            cumulative[date] = capital;
        }

        return TimeBucketService.Get(cumulative.Select(x => (x.Key, x.Value)))
                                 .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                                 .ToList();
    }

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);
        return await GetOwnerClosingBalance(userId, start, end);
    }

    private async Task<List<TimeSeriesModel>> GetOwnerClosingBalance(int userId, DateTime start, DateTime end)
    {
        var walk = new OwnerLedgerWalk(await bondTransactionReader.GetValueRange(userId, start, end));
        if (walk.IsEmpty) return [];

        await PooledBondValuation.LoadDetailsAsync(bondDashboardContext, walk);
        var valuation = new PooledBondValuation(bondDashboardContext, DateOnly.FromDateTime(end));

        // Every day carries an explicit value so a bond sold to zero does not keep yesterday's balance.
        var prices = walk.EachDayEnd(start, end).Select(day =>
            (day.Day, Value: day.Positions.Sum(p => valuation.ValueOn(p, DateOnly.FromDateTime(day.Day)))));

        return TimeBucketService.Get(prices)
                                 .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                                 .ToList();
    }

    private async Task<List<TimeSeriesModel>> AggregateByDay(int userId, DateTime start, DateTime end, Func<decimal, bool> predicate)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);
        return await AggregateOwnerByDay(userId, start, end, predicate);
    }

    private async Task<List<TimeSeriesModel>> AggregateOwnerByDay(int userId, DateTime start, DateTime end, Func<decimal, bool> predicate)
    {
        var points = (await bondTransactionReader.GetValueRange(userId, start, end)).Points
            .Where(p => p.PostingDate.Date >= start.Date && p.PostingDate.Date <= end.Date && predicate(p.ValueChange))
            .ToList();
        var bondDetails = await bondDashboardContext.LoadReferencedDetailsAsync(points.Select(p => p.PartitionId));

        Dictionary<DateTime, decimal> result = [];
        foreach (var point in points)
        {
            if (!bondDetails.TryGetValue(point.PartitionId, out var details))
                throw new InvalidOperationException($"Bond valuation requires details for bond id {point.PartitionId}.");

            var priceAtDate = point.ValueChange * details.UnitValue;
            result[point.PostingDate.Date] = result.GetValueOrDefault(point.PostingDate.Date) + (point.ValueChange >= 0 ? priceAtDate : -Math.Abs(priceAtDate));
        }

        return TimeBucketService.Get(result.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                                 .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Sum()))
                                 .ToList();
    }

    private sealed record BondCapitalFlow(DateTime Date, decimal Amount, Currency Currency);
}