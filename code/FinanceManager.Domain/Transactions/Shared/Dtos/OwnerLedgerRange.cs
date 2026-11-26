namespace FinanceManager.Domain.Transactions.Shared.Dtos;

/// <summary>
/// The inputs to value an owner's ledger over a date range: the pooled balance per partition before the range
/// and every entry inside it ordered by posting date, then entry id.
/// </summary>
public sealed record OwnerLedgerRange(IReadOnlyList<OwnerLedgerPosition> Opening, IReadOnlyList<OwnerLedgerPoint> Points)
{
    public static OwnerLedgerRange Empty { get; } = new([], []);

    public bool IsEmpty => Opening.Count == 0 && Points.Count == 0;
}