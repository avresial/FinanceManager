using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Application.Portfolio.Performance;

/// <summary>
/// Currency-translation effect of holding foreign-currency positions over a period. Each holding is
/// split into intervals between its own quantity changes; an interval contributes its ending native
/// value times the change in the FX rate across it.
/// </summary>
internal static class PortfolioFxEffectCalculator
{
    public static async Task<(decimal Effect, bool Complete)> GetInvestmentEffectAsync(
        IReadOnlyList<PortfolioQuantityChange> quantityChanges,
        IReadOnlyDictionary<long, decimal> openingQuantities,
        IReadOnlyDictionary<long, string> listingCurrencies,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        PortfolioPriceCache prices,
        CancellationToken cancellationToken)
    {
        var targetCurrency = prices.TargetCurrency;
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

    /// <summary>
    /// Each bond is split into intervals between the days its pooled position changes. An interval contributes the position's
    /// native value at its end times the change in the FX rate across it.
    /// </summary>
    public static bool TryGetBondEffect(
        PortfolioBondInputs bonds,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal fxEffect)
    {
        fxEffect = 0m;
        var openingPositions = bonds.Range.Opening.ToDictionary(position => position.PartitionId);
        var changeDatesByBond = bonds.Range.Points
            .Where(point => point.PostingDate.Date >= startDate.Date && point.PostingDate.Date <= endDate.Date)
            .GroupBy(point => point.PartitionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(point => DateTime.SpecifyKind(point.PostingDate.Date, DateTimeKind.Utc)).Distinct().Order().ToList());

        foreach (var bondId in bonds.Walk.PartitionIds)
        {
            if (!bonds.Details.TryGetValue(bondId, out var details))
                return false;

            var position = openingPositions.GetValueOrDefault(bondId);
            var currentDate = startDate;

            foreach (var changeDate in changeDatesByBond.GetValueOrDefault(bondId) ?? [])
            {
                if (changeDate > currentDate && position is not null
                    && !TryAddBondInterval(bonds, position, details, targetCurrency, currentDate, changeDate, ratesByCurrency, ref fxEffect))
                    return false;

                position = bonds.Walk.PositionAt(bondId, changeDate.AddDays(1).AddTicks(-1));
                currentDate = changeDate;
            }

            if (position is not null && endDate > currentDate
                && !TryAddBondInterval(bonds, position, details, targetCurrency, currentDate, endDate, ratesByCurrency, ref fxEffect))
                return false;
        }

        return true;
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
        PortfolioBondInputs bonds,
        OwnerLedgerPosition position,
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

        if (!bonds.Valuation.TryValueOn(position, DateOnly.FromDateTime(endDate), out var endingNativeValue))
            return false;
        if (endingNativeValue == 0m && position.Value != 0m)
            return false;

        fxEffect += endingNativeValue * (endRate - startRate);
        return true;
    }
}