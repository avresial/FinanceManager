using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;

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
    IInvestmentPriceProvider priceProvider) : IInvestmentValuationService
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

        var asOfDate = DateOnly.FromDateTime(asOf);
        var rawHoldingsByAccount = await transactionRepository.GetHoldingsByAccountAsOf(accountIds, asOfDate, ct);
        if (rawHoldingsByAccount.Count == 0) return result;

        // Net holding per (account, listing) as of the date, dropping zero net positions so fully
        // closed holdings never trigger a (wasted, potentially failing) price fetch.
        var holdingsByAccount = new Dictionary<int, Dictionary<long, decimal>>();
        foreach (var (accountId, holdings) in rawHoldingsByAccount)
        {
            var nonZero = holdings.Where(kvp => kvp.Value != 0m).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if (nonZero.Count > 0)
                holdingsByAccount[accountId] = nonZero;
        }

        if (holdingsByAccount.Count == 0) return result;

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

        var startDate = start.Date;
        var endDate = end.Date;
        var startDateOnly = DateOnly.FromDateTime(startDate);
        var endDateOnly = DateOnly.FromDateTime(endDate);

        var inputs = await transactionRepository.GetValuationInputs(accountIds, startDateOnly, endDateOnly, ct);
        if (inputs.OpeningPositions.Count == 0 && inputs.InWindowTrades.Count == 0) return result;

        var openingByAccountListing = inputs.OpeningPositions
            .ToDictionary(p => (p.AccountId, p.AssetListingId), p => p.Quantity);

        var tradesByAccountListing = inputs.InWindowTrades
            .GroupBy(t => (t.AccountId, t.AssetListingId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Retain each (account, listing) pair when its opening holding before the range is non-zero
        // OR it has trades in the range. Exclude positions fully closed before the window so they
        // never trigger unneeded price-series fetches.
        var allKeys = openingByAccountListing.Keys.Union(tradesByAccountListing.Keys).ToList();
        var retainedKeys = allKeys
            .Where(k => (openingByAccountListing.TryGetValue(k, out var openQty) && openQty != 0m)
                || tradesByAccountListing.ContainsKey(k))
            .ToList();
        if (retainedKeys.Count == 0) return result;

        // One price-series fetch per distinct listing across all accounts (target currency already
        // applied). Accounts holding the same instrument share this series instead of re-fetching it.
        var priceSeries = new Dictionary<long, IReadOnlyDictionary<DateTime, decimal>>();
        foreach (var listingId in retainedKeys.Select(k => k.AssetListingId).Distinct())
            priceSeries[listingId] = await priceProvider.GetPricePerUnitSeriesAsync(listingId, targetCurrency, start, end, ct);

        foreach (var accountGroup in retainedKeys.GroupBy(k => k.AccountId))
        {
            var series = BuildAccountSeries(
                accountGroup.Key,
                accountGroup.Select(k => k.AssetListingId),
                openingByAccountListing,
                tradesByAccountListing,
                startDate,
                endDate,
                priceSeries);
            if (series.Count > 0) result[accountGroup.Key] = series;
        }

        return result;
    }

    // Fold one account's transactions against the shared per-listing price series: carry each
    // listing's holding forward across days without a trade and value it at that day's price.
    private static Dictionary<DateTime, decimal> BuildAccountSeries(
        int accountId,
        IEnumerable<long> listingIds,
        IReadOnlyDictionary<(int AccountId, long AssetListingId), decimal> openingByAccountListing,
        IReadOnlyDictionary<(int AccountId, long AssetListingId), List<IInvestmentTransactionRepository.ValuationTradeInput>> tradesByAccountListing,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<long, IReadOnlyDictionary<DateTime, decimal>> priceSeries)
    {
        var result = new Dictionary<DateTime, decimal>();
        var listings = listingIds.ToList();

        var holdings = new Dictionary<long, decimal>();
        var dailyDeltas = new Dictionary<long, Dictionary<DateTime, decimal>>();

        foreach (var listingId in listings)
        {
            holdings[listingId] = openingByAccountListing.GetValueOrDefault((accountId, listingId), 0m);
            if (tradesByAccountListing.TryGetValue((accountId, listingId), out var trades))
            {
                dailyDeltas[listingId] = trades
                    .GroupBy(t => t.TradeDate.ToDateTime(TimeOnly.MinValue))
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.SignedQuantity));
            }
            else
            {
                dailyDeltas[listingId] = [];
            }
        }

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            decimal dayValue = 0m;
            foreach (var listingId in listings)
            {
                if (dailyDeltas[listingId].TryGetValue(date, out var delta))
                    holdings[listingId] += delta;

                var holding = holdings[listingId];
                if (holding == 0m) continue;

                if (priceSeries.TryGetValue(listingId, out var listingPrices)
                    && listingPrices.TryGetValue(date, out var price) && price > 0)
                    dayValue += holding * price;
            }

            // Keep an explicit closing zero so downstream buckets cannot reuse yesterday's
            // value after a final sale or an unavailable closing price.
            if (dayValue != 0m || date == endDate) result[date] = dayValue;
        }

        return result;
    }
}