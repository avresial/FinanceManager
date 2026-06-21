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
        var holdings = await GetHoldingsAsOfAsync(accountId, DateOnly.FromDateTime(asOf), ct);
        if (holdings.Count == 0) return 0m;

        decimal total = 0m;
        foreach (var (listingId, holding) in holdings)
        {
            var price = await priceProvider.GetPricePerUnitAsync(listingId, targetCurrency, asOf, ct);
            if (price > 0) total += holding * price;
        }

        return total;
    }

    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetAccountValueSeriesAsync(
        int accountId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        var result = new Dictionary<DateTime, decimal>();
        if (start == default || end == default || end < start) return result;

        var transactions = await transactionRepository.GetByAccount(accountId, ct);
        if (transactions.Count == 0) return result;

        var startDate = start.Date;
        var endDate = end.Date;
        var startDateOnly = DateOnly.FromDateTime(startDate);
        var endDateOnly = DateOnly.FromDateTime(endDate);

        var byListing = transactions
            .Where(t => t.TradeDate <= endDateOnly)
            .GroupBy(t => t.AssetListingId)
            .ToList();
        if (byListing.Count == 0) return result;

        // For each listing: the running holding carried into the window (everything before start),
        // the per-day signed-quantity deltas inside the window, and the price-per-unit series
        // (already in the target currency). One price-series fetch per distinct listing.
        var holdings = new Dictionary<long, decimal>();
        var dailyDeltas = new Dictionary<long, Dictionary<DateTime, decimal>>();
        var priceSeries = new Dictionary<long, IReadOnlyDictionary<DateTime, decimal>>();

        foreach (var group in byListing)
        {
            var listingId = group.Key;
            holdings[listingId] = group.Where(t => t.TradeDate < startDateOnly).Sum(t => t.SignedQuantity);
            dailyDeltas[listingId] = group
                .Where(t => t.TradeDate >= startDateOnly)
                .GroupBy(t => t.TradeDate.ToDateTime(TimeOnly.MinValue))
                .ToDictionary(g => g.Key, g => g.Sum(t => t.SignedQuantity));
            priceSeries[listingId] = await priceProvider.GetPricePerUnitSeriesAsync(listingId, targetCurrency, start, end, ct);
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

                if (priceSeries[listingId].TryGetValue(date, out var price) && price > 0)
                    dayValue += holding * price;
            }

            if (dayValue != 0m) result[date] = dayValue;
        }

        return result;
    }
}