using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;

namespace FinanceManager.Application.FinancialAccounts.Investments.Valuation;

/// <summary>
/// Values the owner's pooled investment holdings: holdings are computed on read from every
/// <see cref="Domain.FinancialAccounts.Investments.Entities.InvestmentTransaction"/> of the user (linked to an account or not)
/// and each listing is priced through <see cref="IInvestmentPriceProvider"/>. Queries run strictly sequentially because the scoped
/// <c>AppDbContext</c> cannot service concurrent operations.
/// </summary>
internal class InvestmentValuationService(
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider) : IInvestmentValuationService
{
    public async Task<IReadOnlyDictionary<long, decimal>> GetUserValueByListingAsync(
        int userId,
        Currency targetCurrency,
        DateTime asOf,
        CancellationToken ct = default)
    {
        var holdings = await transactionRepository.GetHoldingsByUserAsOf(userId, DateOnly.FromDateTime(asOf), ct);
        var result = new Dictionary<long, decimal>();

        foreach (var holding in holdings)
        {
            // Closed positions never trigger a (wasted, potentially failing) price fetch.
            if (holding.Units == 0m) continue;

            var price = await priceProvider.GetPricePerUnitAsync(holding.AssetListingId, targetCurrency, asOf, ct);
            if (price > 0m)
                result[holding.AssetListingId] = holding.Units * price;
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetUserValueSeriesAsync(
        int userId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        if (start == default || end == default || end < start) return new Dictionary<DateTime, decimal>();

        var startDate = start.Date;
        var endDate = end.Date;
        var inputs = await transactionRepository.GetValuationInputsByUser(
            userId, DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate), ct);

        var opening = inputs.OpeningPositions.ToDictionary(p => p.AssetListingId, p => p.Quantity);
        var tradesByListing = inputs.InWindowTrades
            .GroupBy(t => t.AssetListingId)
            .ToDictionary(g => g.Key, g => g.Select(t => (t.TradeDate, t.SignedQuantity)).ToList());

        // Exclude positions fully closed before the window so they never trigger unneeded price-series fetches.
        var listingIds = opening.Keys.Union(tradesByListing.Keys)
            .Where(id => opening.GetValueOrDefault(id) != 0m || tradesByListing.ContainsKey(id))
            .ToList();
        if (listingIds.Count == 0) return new Dictionary<DateTime, decimal>();

        var priceSeries = new Dictionary<long, IReadOnlyDictionary<DateTime, decimal>>();
        foreach (var listingId in listingIds)
            priceSeries[listingId] = await priceProvider.GetPricePerUnitSeriesAsync(listingId, targetCurrency, start, end, ct);

        return BuildSeries(listingIds, opening, tradesByListing, startDate, endDate, priceSeries);
    }

    // Carry each listing's holding forward across days without a trade and value it at that day's price.
    private static Dictionary<DateTime, decimal> BuildSeries(
        IReadOnlyList<long> listings,
        IReadOnlyDictionary<long, decimal> openingByListing,
        IReadOnlyDictionary<long, List<(DateOnly TradeDate, decimal SignedQuantity)>> tradesByListing,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<long, IReadOnlyDictionary<DateTime, decimal>> priceSeries)
    {
        var result = new Dictionary<DateTime, decimal>();

        var holdings = new Dictionary<long, decimal>();
        var dailyDeltas = new Dictionary<long, Dictionary<DateTime, decimal>>();

        foreach (var listingId in listings)
        {
            holdings[listingId] = openingByListing.GetValueOrDefault(listingId, 0m);
            dailyDeltas[listingId] = tradesByListing.TryGetValue(listingId, out var trades)
                ? trades
                    .GroupBy(t => t.TradeDate.ToDateTime(TimeOnly.MinValue))
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.SignedQuantity))
                : [];
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