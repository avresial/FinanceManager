namespace FinanceManager.Domain.Transactions.Shared.Dtos;

/// <summary>
/// The pooled balance of one partition of an owner's ledger: the sum of <c>ValueChange</c> up to a point in time,
/// and the posting date of the latest entry included in it. The partition is the currency for cash and the bond definition for bonds.
/// </summary>
public sealed record OwnerLedgerPosition(int PartitionId, decimal Value, DateTime LastPostingDate);