using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.FinancialAccounts.Investments.Valuation;

/// <summary>
/// Values an investment account by computing holdings on read from its
/// <see cref="Domain.FinancialAccounts.Investments.Entities.InvestmentTransaction"/> rows and
/// pricing each listing through <see cref="IInvestmentPriceProvider"/>.
/// </summary>
/// <remarks>
/// The single-account overloads are thin wrappers over the batched overloads (passing a
/// single-element collection). The batched implementations issue one transactions query for all
/// requested accounts and price each distinct <c>AssetListing</c> once across the whole set, so a
/// dashboard paint no longer re-prices the same listing once per owning account. Queries are still
/// issued strictly sequentially: the scoped <c>AppDbContext</c> cannot service concurrent operations,
/// so the win comes from doing fewer, wider queries — never from parallel awaits.
/// </remarks>
internal class InvestmentValuationService(
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider,
    IInflationIndexProvider inflationIndexProvider) : IInvestmentValuationService
{
    public async Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsOfAsync(int accountId, DateOnly asOf, CancellationToken ct = default)
    {
        var holdings = await transactionRepository.GetHoldingsAsOf([accountId], asOf, ct);
        return holdings
            .Where(kvp => kvp.Value != 0m)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public async Task<decimal> GetAccountValueAsync(int accountId, Currency targetCurrency, DateTime asOf, CancellationToken ct = default)
    {
        var holdings = await transactionRepository.GetHoldingsAsOf([accountId], DateOnly.FromDateTime(asOf), ct);
        decimal total = 0m;

        foreach (var (listingId, holding) in holdings)
        {
            if (holding == 0m) continue;

            var price = await priceProvider.GetPricePerUnitAsync(listingId, targetCurrency, asOf, ct);
            if (price > 0m)
                total += holding * price;
        }

        return total;
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetAccountValueAsync(
        IReadOnlyCollection<int> accountIds,
        Currency targetCurrency,
        DateTime asOf,
        CancellationToken ct = default)
    {
        var result = new Dictionary<int, decimal>();
        if (accountIds.Count == 0) return result;

        var transactions = await transactionRepository.GetByAccounts(accountIds, ct);
        if (transactions.Count == 0) return result;

        var asOfDate = DateOnly.FromDateTime(asOf);

        // Net holding per (account, listing) as of the date, dropping zero net positions so fully
        // closed holdings never trigger a (wasted, potentially failing) price fetch.
        var holdingsByAccount = new Dictionary<int, Dictionary<long, decimal>>();
        foreach (var group in transactions.Where(t => t.TradeDate <= asOfDate).GroupBy(t => t.AccountId))
        {
            var perListing = group
                .GroupBy(t => t.AssetListingId)
                .Select(g => (ListingId: g.Key, Holding: g.Sum(t => t.SignedQuantity)))
                .Where(x => x.Holding != 0m)
                .ToDictionary(x => x.ListingId, x => x.Holding);
            if (perListing.Count > 0) holdingsByAccount[group.Key] = perListing;
        }

        // Price each distinct listing across all accounts once, not once per owning account.
        var prices = new Dictionary<long, decimal>();
        foreach (var listingId in holdingsByAccount.Values.SelectMany(h => h.Keys).Distinct())
            prices[listingId] = await priceProvider.GetPricePerUnitAsync(listingId, targetCurrency, asOf, ct);

        foreach (var (accountId, holdings) in holdingsByAccount)
        {
            decimal total = 0m;
            foreach (var (listingId, holding) in holdings)
            {
                if (prices.TryGetValue(listingId, out var price) && price > 0)
                    total += holding * price;
            }

            if (total != 0m) result[accountId] = total;
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetAccountValueSeriesAsync(
        int accountId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        var byAccount = await GetAccountValueSeriesAsync([accountId], targetCurrency, start, end, ct);
        return byAccount.TryGetValue(accountId, out var series) ? series : new Dictionary<DateTime, decimal>();
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyDictionary<DateTime, decimal>>> GetAccountValueSeriesAsync(
        IReadOnlyCollection<int> accountIds,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        var result = new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>>();
        if (accountIds.Count == 0 || start == default || end == default || end < start) return result;

        var transactions = await transactionRepository.GetByAccounts(accountIds, ct);
        if (transactions.Count == 0) return result;

        var startDate = start.Date;
        var endDate = end.Date;
        var startDateOnly = DateOnly.FromDateTime(startDate);
        var endDateOnly = DateOnly.FromDateTime(endDate);

        var relevant = transactions.Where(t => t.TradeDate <= endDateOnly).ToList();
        if (relevant.Count == 0) return result;

        // One price-series fetch per distinct listing across all accounts (target currency already
        // applied). Accounts holding the same instrument share this series instead of re-fetching it.
        var priceSeries = new Dictionary<long, IReadOnlyDictionary<DateTime, decimal>>();
        foreach (var listingId in relevant.Select(t => t.AssetListingId).Distinct())
            priceSeries[listingId] = await priceProvider.GetPricePerUnitSeriesAsync(listingId, targetCurrency, start, end, ct);

        foreach (var accountGroup in relevant.GroupBy(t => t.AccountId))
        {
            var series = BuildAccountSeries(accountGroup, startDate, endDate, startDateOnly, priceSeries);
            if (series.Count > 0) result[accountGroup.Key] = series;
        }

        return result;
    }

    // Fold one account's transactions against the shared per-listing price series: carry each
    // listing's holding forward across days without a trade and value it at that day's price.
    private static Dictionary<DateTime, decimal> BuildAccountSeries(
        IEnumerable<InvestmentTransaction> accountTransactions,
        DateTime startDate,
        DateTime endDate,
        DateOnly startDateOnly,
        IReadOnlyDictionary<long, IReadOnlyDictionary<DateTime, decimal>> priceSeries)
    {
        var result = new Dictionary<DateTime, decimal>();

        var byListing = accountTransactions.GroupBy(t => t.AssetListingId).ToList();

        var holdings = new Dictionary<long, decimal>();
        var dailyDeltas = new Dictionary<long, Dictionary<DateTime, decimal>>();

        foreach (var group in byListing)
        {
            var listingId = group.Key;
            holdings[listingId] = group.Where(t => t.TradeDate < startDateOnly).Sum(t => t.SignedQuantity);
            dailyDeltas[listingId] = group
                .Where(t => t.TradeDate >= startDateOnly)
                .GroupBy(t => t.TradeDate.ToDateTime(TimeOnly.MinValue))
                .ToDictionary(g => g.Key, g => g.Sum(t => t.SignedQuantity));
        }

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            decimal dayValue = 0m;
            foreach (var group in byListing)
            {
                var listingId = group.Key;
                if (dailyDeltas[listingId].TryGetValue(date, out var delta))
                    holdings[listingId] += delta;

                var holding = holdings[listingId];
                if (holding == 0m) continue;

                if (priceSeries.TryGetValue(listingId, out var listingPrices)
                    && listingPrices.TryGetValue(date, out var price) && price > 0)
                    dayValue += holding * price;
            }

            if (dayValue != 0m) result[date] = dayValue;
        }

        return result;
    }

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

        var transactions = (await transactionRepository.GetByAccounts([accountId], ct))
            .Where(t => t.TradeDate <= endDateOnly)
            .ToList();
        if (transactions.Count == 0) return new Dictionary<DateTime, decimal>();

        var raw = assetListingId is long listingId
            ? await priceProvider.GetPricePerUnitSeriesAsync(listingId, targetCurrency, start, end, ct)
            : await inflationIndexProvider.GetIndexSeriesAsync(start, end, ct);
        var index = ToDailySeries(raw, startDate, endDate);
        if (index.Count == 0) return new Dictionary<DateTime, decimal>();

        // Contributions are measured at the same market prices the account's own value series uses,
        // so a day's contribution equals the value that trade added to the account that day.
        var priceSeries = new Dictionary<long, Dictionary<DateTime, decimal>>();
        foreach (var id in transactions.Select(t => t.AssetListingId).Distinct())
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

        return BuildContributionMatchedSeries(transactions, index, priceSeries, startDate, endDate, startDateOnly);
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
        IEnumerable<InvestmentTransaction> transactions,
        IReadOnlyDictionary<DateTime, decimal> index,
        IReadOnlyDictionary<long, Dictionary<DateTime, decimal>> priceSeries,
        DateTime startDate,
        DateTime endDate,
        DateOnly startDateOnly)
    {
        var byDate = transactions
            .Where(t => t.TradeDate >= startDateOnly)
            .GroupBy(t => t.TradeDate.ToDateTime(TimeOnly.MinValue))
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(t => t.AssetListingId)
                    .Select(l => (ListingId: l.Key, Quantity: l.Sum(t => t.SignedQuantity)))
                    .ToList());

        var openingValue = transactions
            .Where(t => t.TradeDate < startDateOnly)
            .GroupBy(t => t.AssetListingId)
            .Sum(g => g.Sum(t => t.SignedQuantity) * PriceOn(priceSeries, g.Key, startDate));

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