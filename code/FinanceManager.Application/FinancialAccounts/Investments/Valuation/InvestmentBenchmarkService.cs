using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.FinancialAccounts.Investments.Valuation;

/// <summary>
/// Builds a contribution-matched shadow portfolio that invests an account's cash flows in a benchmark
/// (a listing or the default inflation index) instead of the account's own holdings.
/// </summary>
internal class InvestmentBenchmarkService(
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider,
    IInflationIndexProvider inflationIndexProvider) : IInvestmentBenchmarkService
{
    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetBenchmarkSeriesAsync(
        long? assetListingId,
        int accountId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        if (start == default || end == default || end < start)
            return new Dictionary<DateTime, decimal>();

        var startDate = start.Date;
        var endDate = end.Date;
        var startDateOnly = DateOnly.FromDateTime(startDate);
        var endDateOnly = DateOnly.FromDateTime(endDate);

        var inputs = await transactionRepository.GetValuationInputs([accountId], startDateOnly, endDateOnly, ct);
        if (inputs.OpeningPositions.Count == 0 && inputs.InWindowTrades.Count == 0)
            return new Dictionary<DateTime, decimal>();

        var raw = assetListingId is long listingId
            ? await priceProvider.GetPricePerUnitSeriesAsync(listingId, targetCurrency, start, end, ct)
            : await inflationIndexProvider.GetIndexSeriesAsync(start, end, ct);
        var index = ToDailySeries(raw, startDate, endDate);
        if (index.Count == 0) return new Dictionary<DateTime, decimal>();

        var relevantListingIds = inputs.OpeningPositions
            .Select(p => p.AssetListingId)
            .Union(inputs.InWindowTrades.Select(t => t.AssetListingId))
            .Distinct()
            .ToList();

        // Contributions are measured at the same market prices the account's own value series uses,
        // so a day's contribution equals the value that trade added to the account that day.
        var priceSeries = new Dictionary<long, Dictionary<DateTime, decimal>>();
        foreach (var id in relevantListingIds)
        {
            // Benchmarking against an instrument the account also holds would otherwise fetch that
            // listing's prices twice over the same range — the benchmark's own series is identical.
            priceSeries[id] = assetListingId == id
                ? index
                : ToDailySeries(
                    await priceProvider.GetPricePerUnitSeriesAsync(id, targetCurrency, start, end, ct),
                    startDate,
                    endDate);
        }

        return BuildContributionMatchedSeries(inputs, index, priceSeries, startDate, endDate);
    }

    // Both the benchmark index and instrument prices are published on their own calendars — monthly
    // for inflation, trading days for a listing — so carry the latest published level forward across
    // the gaps. Days before the first published point take that first level, which keeps a range that
    // opens on a weekend or ahead of the first print from dropping its opening value.
    private static Dictionary<DateTime, decimal> ToDailySeries(
        IReadOnlyDictionary<DateTime, decimal> raw,
        DateTime startDate,
        DateTime endDate)
    {
        var result = new Dictionary<DateTime, decimal>();
        var published = raw
            .Where(x => x.Value > 0)
            .Select(x => (Date: x.Key.Date, x.Value))
            .OrderBy(x => x.Date)
            .ToList();
        if (published.Count == 0) return result;

        var next = 0;
        var level = published[0].Value;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            while (next < published.Count && published[next].Date <= date)
                level = published[next++].Value;
            result[date] = level;
        }

        return result;
    }

    // Fold the account's cash flows into benchmark units: seed with whatever the account already held
    // when the range opened, then buy or sell units as trades move money in and out.
    private static Dictionary<DateTime, decimal> BuildContributionMatchedSeries(
        IInvestmentTransactionRepository.AccountValuationInputs inputs,
        IReadOnlyDictionary<DateTime, decimal> index,
        IReadOnlyDictionary<long, Dictionary<DateTime, decimal>> priceSeries,
        DateTime startDate,
        DateTime endDate)
    {
        var byDate = inputs.InWindowTrades
            .GroupBy(t => t.TradeDate.ToDateTime(TimeOnly.MinValue))
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(t => t.AssetListingId)
                    .Select(l => (ListingId: l.Key, Quantity: l.Sum(t => t.SignedQuantity)))
                    .ToList());

        var openingValue = inputs.OpeningPositions
            .Sum(p => p.Quantity * PriceOn(priceSeries, p.AssetListingId, startDate));

        var result = new Dictionary<DateTime, decimal>();
        decimal units = 0m;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var level = index[date];

            if (date == startDate && openingValue > 0m)
                units += openingValue / level;

            if (byDate.TryGetValue(date, out var dayTrades))
            {
                var contribution = dayTrades.Sum(t => t.Quantity * PriceOn(priceSeries, t.ListingId, date));
                if (contribution != 0m) units += contribution / level;
            }

            var value = units * level;
            if (value > 0m) result[date] = value;
        }

        return result;
    }

    private static decimal PriceOn(
        IReadOnlyDictionary<long, Dictionary<DateTime, decimal>> priceSeries,
        long listingId,
        DateTime date) =>
        priceSeries.TryGetValue(listingId, out var series) && series.TryGetValue(date, out var price)
            ? price
            : 0m;
}