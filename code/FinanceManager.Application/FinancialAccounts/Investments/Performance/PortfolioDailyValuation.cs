using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>
/// Day-by-day value of the investment and bond holdings in one target currency, used to build
/// time-weighted return periods. Any day that cannot be priced makes the whole series incomplete.
/// </summary>
internal static class PortfolioDailyValuation
{
    public static async Task<PortfolioValuationSeries> BuildInvestmentValuesAsync(
        IInvestmentPriceProvider investmentPriceProvider,
        IInvestmentTransactionRepository.AccountValuationInputs inputs,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var openingHoldings = inputs.OpeningPositions
            .GroupBy(position => (position.AccountId, position.AssetListingId))
            .ToDictionary(group => group.Key, group => group.Sum(position => position.Quantity));
        var tradesByDate = inputs.InWindowTrades
            .GroupBy(trade => trade.TradeDate)
            .ToDictionary(
                group => group.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                group => group
                    .GroupBy(trade => (trade.AccountId, trade.AssetListingId))
                    .ToDictionary(tradeGroup => tradeGroup.Key, tradeGroup => tradeGroup.Sum(trade => trade.SignedQuantity)));

        var listingIds = openingHoldings.Keys
            .Select(key => key.AssetListingId)
            .Concat(tradesByDate.Values.SelectMany(day => day.Keys).Select(key => key.AssetListingId))
            .Distinct()
            .ToList();
        var priceSeries = new Dictionary<long, IReadOnlyDictionary<DateTime, decimal>>();
        foreach (var listingId in listingIds)
        {
            priceSeries[listingId] = await investmentPriceProvider.GetPricePerUnitSeriesAsync(
                listingId,
                targetCurrency,
                startDate,
                endDate,
                cancellationToken);
        }

        decimal openingValue = 0m;
        foreach (var (key, quantity) in openingHoldings)
        {
            if (quantity == 0m) continue;
            if (!TryGetPrice(priceSeries, key.AssetListingId, startDate, out var price))
                return PortfolioValuationSeries.Incomplete;

            openingValue += quantity * price;
        }

        var holdings = openingHoldings.ToDictionary();
        Dictionary<DateTime, decimal> values = [];
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (tradesByDate.TryGetValue(date, out var dailyTrades))
            {
                foreach (var (key, quantity) in dailyTrades)
                    holdings[key] = holdings.GetValueOrDefault(key) + quantity;
            }

            decimal value = 0m;
            foreach (var (key, quantity) in holdings)
            {
                if (quantity == 0m) continue;
                if (!TryGetPrice(priceSeries, key.AssetListingId, date, out var price))
                    return PortfolioValuationSeries.Incomplete;

                value += quantity * price;
            }

            values[date] = value;
        }

        return new(values, openingValue, true);
    }

    public static PortfolioValuationSeries BuildBondValues(
        IReadOnlyList<BondAccount> accounts,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate)
    {
        var entriesByAccount = accounts
            .Select(account => account.Entries
                .Concat(account.NextOlderEntries.Values)
                .GroupBy(entry => entry.BondDetailsId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<BondAccountEntry>)group
                        .OrderByDescending(entry => entry.PostingDate)
                        .ThenByDescending(entry => entry.EntryId)
                        .ToList()))
            .ToList();

        decimal openingValue = 0m;
        foreach (var entries in entriesByAccount)
        {
            var result = GetBondValueAt(
                entries,
                bondDetails,
                ratesByCurrency,
                targetCurrency,
                DateOnly.FromDateTime(startDate),
                includeDate: false);
            if (!result.Complete)
                return PortfolioValuationSeries.Incomplete;

            openingValue += result.Value;
        }

        Dictionary<DateTime, decimal> values = [];
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            decimal value = 0m;
            foreach (var entries in entriesByAccount)
            {
                var result = GetBondValueAt(
                    entries,
                    bondDetails,
                    ratesByCurrency,
                    targetCurrency,
                    DateOnly.FromDateTime(date),
                    includeDate: true);
                if (!result.Complete)
                    return PortfolioValuationSeries.Incomplete;

                value += result.Value;
            }

            values[date] = value;
        }

        return new(values, openingValue, true);
    }

    private static (decimal Value, bool Complete) GetBondValueAt(
        IReadOnlyDictionary<int, IReadOnlyList<BondAccountEntry>> entriesByBond,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        Currency targetCurrency,
        DateOnly valuationDate,
        bool includeDate)
    {
        decimal total = 0m;
        foreach (var entries in entriesByBond.Values)
        {
            var entry = entries
                .Where(candidate =>
                {
                    var postingDate = DateOnly.FromDateTime(candidate.PostingDate);
                    return includeDate ? postingDate <= valuationDate : postingDate < valuationDate;
                })
                .FirstOrDefault();
            if (entry is null || entry.Value == 0m) continue;

            if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details))
                return (0m, false);

            var value = entry.GetPriceAt(valuationDate, details);
            if (value <= 0m)
                return (0m, false);

            if (!TryConvert(
                    value,
                    details.Currency.ShortName,
                    valuationDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    targetCurrency,
                    ratesByCurrency,
                    out var converted))
                return (0m, false);

            total += converted;
        }

        return (total, true);
    }

    private static bool TryGetPrice(
        IReadOnlyDictionary<long, IReadOnlyDictionary<DateTime, decimal>> priceSeries,
        long listingId,
        DateTime date,
        out decimal price)
    {
        price = 0m;
        return priceSeries.TryGetValue(listingId, out var listingPrices)
               && listingPrices.TryGetValue(date.Date, out price)
               && price > 0m;
    }

    /// <summary>
    /// Converts with the rate on <paramref name="date"/>. The source code is trimmed before lookup; a
    /// missing or non-positive rate returns false, even for a zero amount.
    /// </summary>
    public static bool TryConvert(
        decimal amount,
        string sourceCurrency,
        DateTime date,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal converted)
    {
        if (string.Equals(sourceCurrency.Trim(), targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
        {
            converted = amount;
            return true;
        }

        if (ratesByCurrency.TryGetValue(sourceCurrency.Trim(), out var rates)
            && CurrencyRateSeries.TryGet(rates, date, out var rate)
            && rate > 0m)
        {
            converted = amount * rate;
            return true;
        }

        converted = 0m;
        return false;
    }
}