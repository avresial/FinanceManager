using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.Transactions.Shared;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Application.FinancialAccounts.Bond.Valuation;

/// <summary>
/// Values owner-pooled bond positions for one operation. A position's units are the pooled balance of a bond definition and
/// interest accrues from the latest pooled entry's posting date, so a later purchase restarts accrual for every unit.
/// Daily prices of one (bond, anchor, units) combination come from a single history computed through the operation's last day,
/// which keeps a long-held bond from being re-accrued from its anchor on every day of a range. The histories belong to this
/// instance only; prices are also memoized in the request-scoped <see cref="BondDashboardContext"/> shared by the dashboard's services.
/// </summary>
internal sealed class PooledBondValuation(BondDashboardContext context, DateOnly throughDate)
{
    private readonly Dictionary<(int BondDetailsId, DateOnly AnchorDate, decimal Units), Dictionary<DateOnly, decimal>> _histories = [];

    /// <summary>Loads the definitions of every bond the given positions hold, so valuation can run synchronously.</summary>
    public static Task LoadDetailsAsync(BondDashboardContext context, IEnumerable<OwnerLedgerPosition> positions, CancellationToken ct = default) =>
        context.LoadReferencedDetailsAsync(positions.Where(p => p.Value != 0m).Select(p => p.PartitionId), ct);

    /// <summary>Loads the definitions of every bond that has an opening balance or an entry in the walked range.</summary>
    public static Task LoadDetailsAsync(BondDashboardContext context, OwnerLedgerWalk walk, CancellationToken ct = default) =>
        context.LoadReferencedDetailsAsync(walk.PartitionIds, ct);

    /// <summary>
    /// The value of the position on <paramref name="date"/>. Throws the existing <see cref="InvalidOperationException"/> when an active
    /// (non-zero) position has no definition or no calculation methods.
    /// </summary>
    public decimal ValueOn(OwnerLedgerPosition position, DateOnly date)
    {
        if (position.Value == 0m) return 0m;

        var anchorDate = DateOnly.FromDateTime(position.LastPostingDate);
        return context.GetOrComputePrice(position.PartitionId, anchorDate, position.Value, date, () =>
        {
            if (date < anchorDate) return 0m;

            var details = context.GetDetails(position.PartitionId);
            var key = (position.PartitionId, anchorDate, position.Value);
            if (!_histories.TryGetValue(key, out var history) || date > throughDate)
            {
                history = BondAccountEntry.GetPrice(anchorDate, position.Value, date > throughDate ? date : throughDate, details);
                _histories[key] = history;
            }

            return history.GetValueOrDefault(date);
        });
    }
}