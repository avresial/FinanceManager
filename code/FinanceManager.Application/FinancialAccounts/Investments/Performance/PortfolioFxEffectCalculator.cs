using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>
/// Currency-translation effect of holding foreign-currency positions over a period. Each holding is
/// split into intervals between its own quantity changes; an interval contributes its ending native
/// value times the change in the FX rate across it.
/// </summary>
internal static class PortfolioFxEffectCalculator
{
    public static async Task<(decimal Effect, bool Complete)> GetInvestmentEffectAsync(
        IReadOnlyList<PortfolioQuantityChange> quantityChanges,
        IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>> openingHoldings,
        IReadOnlyDictionary<long, string> listingCurrencies,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        PortfolioPriceCache prices,
        CancellationToken cancellationToken)
    {
        var targetCurrency = prices.TargetCurrency;
        var openingQuantities = new Dictionary<long, decimal>();
        foreach (var holdings in openingHoldings.Values)
        {
            foreach (var (listingId, quantity) in holdings)
            {
                openingQuantities[listingId] = openingQuantities.GetValueOrDefault(listingId) + quantity;
            }
        }

        var flowsByListing = quantityChanges
            .GroupBy(flow => flow.InstrumentId)
            .ToDictionary(group => group.Key, group => group.OrderBy(flow => flow.Date).ThenBy(flow => flow.SourceId).ToList());

        decimal fxEffect = 0m;
        foreach (var listingId in openingQuantities.Keys.Concat(flowsByListing.Keys).Distinct())
        {
            if (!listingCurrencies.TryGetValue(listingId, out var sourceCurrency))
                return (0m, false);
            if (string.Equals(sourceCurrency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
                continue;

            var quantity = openingQuantities.GetValueOrDefault(listingId);
            var currentDate = startDate;
            if (flowsByListing.TryGetValue(listingId, out var flows))
            {
                foreach (var flow in flows)
                {
                    var flowDate = flow.Date;
                    if (flowDate > currentDate && quantity != 0m)
                    {
                        var interval = await GetInvestmentIntervalAsync(
                            listingId, quantity, sourceCurrency, currentDate, flowDate, ratesByCurrency, prices, cancellationToken);
                        if (!interval.Complete)
                            return (0m, false);

                        fxEffect += interval.Effect;
                    }

                    quantity += flow.Kind == PortfolioMovementKind.Purchase ? flow.Quantity : -flow.Quantity;
                    currentDate = flowDate;
                }
            }

            if (endDate > currentDate && quantity != 0m)
            {
                var interval = await GetInvestmentIntervalAsync(
                    listingId, quantity, sourceCurrency, currentDate, endDate, ratesByCurrency, prices, cancellationToken);
                if (!interval.Complete)
                    return (0m, false);

                fxEffect += interval.Effect;
            }
        }

        return (fxEffect, true);
    }

    public static bool TryGetBondEffect(
        IEnumerable<BondAccount> accounts,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal fxEffect)
    {
        fxEffect = 0m;
        foreach (var account in accounts)
        {
            var entriesByBond = account.Entries
                .Concat(account.NextOlderEntries.Values)
                .GroupBy(entry => entry.BondDetailsId);

            foreach (var bondEntries in entriesByBond)
            {
                if (!bondDetails.TryGetValue(bondEntries.Key, out var details))
                    return false;

                var entries = bondEntries
                    .GroupBy(entry => entry.EntryId)
                    .Select(group => group.First())
                    .OrderBy(entry => entry.PostingDate)
                    .ThenBy(entry => entry.EntryId)
                    .ToList();
                var previousEntry = entries.LastOrDefault(entry => entry.PostingDate.Date < startDate.Date);
                var currentDate = startDate;

                foreach (var entry in entries.Where(entry => entry.PostingDate.Date >= startDate.Date && entry.PostingDate.Date <= endDate.Date))
                {
                    var entryDate = DateTime.SpecifyKind(entry.PostingDate.Date, DateTimeKind.Utc);
                    if (entryDate > currentDate && previousEntry is not null
                        && !TryAddBondInterval(previousEntry, details, targetCurrency, currentDate, entryDate, ratesByCurrency, ref fxEffect))
                        return false;

                    previousEntry = entry;
                    currentDate = entryDate;
                }

                if (previousEntry is not null && endDate > currentDate
                    && !TryAddBondInterval(previousEntry, details, targetCurrency, currentDate, endDate, ratesByCurrency, ref fxEffect))
                    return false;
            }
        }

        return true;
    }

    /// <summary>Bond entry value on <paramref name="date"/>; false when the bond cannot be priced on that date.</summary>
    public static bool TryGetBondPrice(
        BondAccountEntry entry,
        BondDetails details,
        DateOnly date,
        out decimal value)
    {
        try
        {
            value = entry.GetPriceAt(date, details);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            value = 0m;
            return false;
        }
    }

    private static async Task<(decimal Effect, bool Complete)> GetInvestmentIntervalAsync(
        long listingId,
        decimal quantity,
        string sourceCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        PortfolioPriceCache prices,
        CancellationToken cancellationToken)
    {
        if (!PortfolioRates.TryGetRatePair(sourceCurrency, prices.TargetCurrency, startDate, endDate, ratesByCurrency, out var startRate, out var endRate))
            return (0m, false);

        var endingTargetPrice = await prices.GetAsync(listingId, endDate, cancellationToken);
        if (endingTargetPrice <= 0m)
            return (0m, false);

        var endingNativePrice = endingTargetPrice / endRate;
        return (quantity * endingNativePrice * (endRate - startRate), true);
    }

    private static bool TryAddBondInterval(
        BondAccountEntry entry,
        BondDetails details,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        ref decimal fxEffect)
    {
        var sourceCurrency = PortfolioRates.NormalizeCurrency(details.Currency.ShortName);
        if (!PortfolioRates.TryGetRatePair(sourceCurrency, targetCurrency, startDate, endDate, ratesByCurrency, out var startRate, out var endRate))
            return false;

        if (!TryGetBondPrice(entry, details, DateOnly.FromDateTime(endDate), out var endingNativeValue))
            return false;
        if (endingNativeValue == 0m && entry.Value != 0m)
            return false;

        fxEffect += endingNativeValue * (endRate - startRate);
        return true;
    }
}