using FinanceManager.Application.Currencies.ExchangeRates;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;

namespace FinanceManager.Application.Portfolio.Performance;

/// <summary>
/// Day-by-day value of the investment and bond holdings in one target currency, used to build
/// time-weighted return periods. Any day that cannot be priced makes the whole series incomplete.
/// </summary>
internal static class PortfolioDailyValuation
{
    public static async Task<PortfolioValuationSeries> BuildInvestmentValuesAsync(
        IInvestmentPriceProvider investmentPriceProvider,
        IInvestmentTransactionRepository.UserValuationInputs inputs,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var openingHoldings = inputs.OpeningPositions
            .GroupBy(position => position.AssetListingId)
            .ToDictionary(group => group.Key, group => group.Sum(position => position.Quantity));
        var tradesByDate = inputs.InWindowTrades
            .GroupBy(trade => trade.TradeDate)
            .ToDictionary(
                group => group.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                group => group
                    .GroupBy(trade => trade.AssetListingId)
                    .ToDictionary(tradeGroup => tradeGroup.Key, tradeGroup => tradeGroup.Sum(trade => trade.SignedQuantity)));

        var listingIds = openingHoldings.Keys
            .Concat(tradesByDate.Values.SelectMany(day => day.Keys))
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
        foreach (var (listingId, quantity) in openingHoldings)
        {
            if (quantity == 0m) continue;
            if (!TryGetPrice(priceSeries, listingId, startDate, out var price))
                return PortfolioValuationSeries.Incomplete;

            openingValue += quantity * price;
        }

        var holdings = openingHoldings.ToDictionary();
        Dictionary<DateTime, decimal> values = [];
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (tradesByDate.TryGetValue(date, out var dailyTrades))
            {
                foreach (var (listingId, quantity) in dailyTrades)
                    holdings[listingId] = holdings.GetValueOrDefault(listingId) + quantity;
            }

            decimal value = 0m;
            foreach (var (listingId, quantity) in holdings)
            {
                if (quantity == 0m) continue;
                if (!TryGetPrice(priceSeries, listingId, date, out var price))
                    return PortfolioValuationSeries.Incomplete;

                value += quantity * price;
            }

            values[date] = value;
        }

        return new(values, openingValue, true);
    }

    /// <summary>
    /// Pooled bond value per day: the opening value is the position before <paramref name="startDate"/> priced on it, each day is
    /// priced from the positions as they stand at the end of that day.
    /// </summary>
    public static PortfolioValuationSeries BuildBondValues(
        PortfolioBondInputs bonds,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate)
    {
        if (!bonds.TryValue(bonds.Range.Opening, ratesByCurrency, targetCurrency, DateOnly.FromDateTime(startDate), out var openingValue))
            return PortfolioValuationSeries.Incomplete;

        Dictionary<DateTime, decimal> values = [];
        foreach (var (day, positions) in bonds.Walk.EachDayEnd(startDate, endDate))
        {
            if (!bonds.TryValue(positions, ratesByCurrency, targetCurrency, DateOnly.FromDateTime(day), out var value))
                return PortfolioValuationSeries.Incomplete;

            values[day] = value;
        }

        return new(values, openingValue, true);
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