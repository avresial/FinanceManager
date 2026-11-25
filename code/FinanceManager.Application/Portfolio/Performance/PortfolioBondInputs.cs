using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Shared;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Application.Portfolio.Performance;

/// <summary>
/// The owner's pooled bond ledger for one performance calculation: opening balances plus every entry in the period, the shared
/// running-balance walk over them, and the single pooled valuation that prices its positions. MWR, TWR, attribution and the FX
/// effect only convert what this prices, so bond arithmetic lives in one place.
/// </summary>
internal sealed record PortfolioBondInputs(
    OwnerLedgerRange Range,
    OwnerLedgerWalk Walk,
    IReadOnlyDictionary<int, BondDetails> Details,
    PooledBondValuation Valuation,
    DateTime EndInstant)
{
    public bool IsEmpty => Range.IsEmpty;

    /// <summary>False when a bond with an opening balance or an entry in the period has no stored definition.</summary>
    public bool HasAllDetails => Walk.PartitionIds.All(Details.ContainsKey);

    /// <summary>The pooled positions at the end of the period's last day.</summary>
    public IReadOnlyList<OwnerLedgerPosition> EndingPositions => Walk.At(EndInstant);

    /// <summary>
    /// Loads the bond ledger for [<paramref name="startDate"/>, <paramref name="endDate"/>]. Entries posted at any time on
    /// <paramref name="endDate"/> belong to the period, so the range ends with that UTC day.
    /// </summary>
    public static async Task<PortfolioBondInputs> LoadAsync(
        IBondTransactionReader bondTransactionReader,
        BondDashboardContext bondDashboardContext,
        int userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var endInstant = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        var range = await bondTransactionReader.GetValueRange(
            userId, DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc), endInstant, cancellationToken);
        var walk = new OwnerLedgerWalk(range);
        var details = await bondDashboardContext.LoadReferencedDetailsAsync(walk.PartitionIds, cancellationToken);
        return new(range, walk, details, new PooledBondValuation(bondDashboardContext, DateOnly.FromDateTime(endDate)), endInstant);
    }

    /// <summary>
    /// Total value of the positions on <paramref name="valuationDate"/> in the target currency. False when a held bond has no
    /// definition, cannot be priced or priced to zero, or its currency has no rate on that date.
    /// </summary>
    public bool TryValue(
        IEnumerable<OwnerLedgerPosition> positions,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        Currency targetCurrency,
        DateOnly valuationDate,
        out decimal total)
    {
        total = 0m;
        foreach (var position in positions)
        {
            if (position.Value == 0m) continue;

            if (!Details.TryGetValue(position.PartitionId, out var details)
                || !Valuation.TryValueOn(position, valuationDate, out var value)
                || value == 0m
                || !PortfolioDailyValuation.TryConvert(
                    value,
                    PortfolioRates.NormalizeCurrency(details.Currency.ShortName),
                    valuationDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    targetCurrency,
                    ratesByCurrency,
                    out var converted))
            {
                total = 0m;
                return false;
            }

            total += converted;
        }

        return true;
    }
}