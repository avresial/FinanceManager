namespace FinanceManager.Domain.Transactions.Shared.Dtos;

/// <summary>One ledger entry inside a requested range: its partition, ordering key and signed change.</summary>
public sealed record OwnerLedgerPoint(int PartitionId, long EntryId, DateTime PostingDate, decimal ValueChange);