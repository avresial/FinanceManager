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
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        decimal baseValue,
        CancellationToken ct = default)
    {
        if (baseValue <= 0 || start == default || end == default || end < start)
            return new Dictionary<DateTime, decimal>();

        var raw = assetListingId is long listingId
            ? await priceProvider.GetPricePerUnitSeriesAsync(listingId, targetCurrency, start, end, ct)
            : await inflationIndexProvider.GetIndexSeriesAsync(start, end, ct);
        var ordered = raw.Where(x => x.Value > 0).OrderBy(x => x.Key).ToList();
        if (ordered.Count == 0) return new Dictionary<DateTime, decimal>();

        var firstValue = ordered[0].Value;
        return ordered.ToDictionary(x => x.Key, x => baseValue * x.Value / firstValue);
    }
}
