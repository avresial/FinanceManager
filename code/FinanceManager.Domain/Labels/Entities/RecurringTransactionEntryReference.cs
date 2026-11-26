namespace FinanceManager.Domain.Labels.Entities;

/// <summary>The cash entry behind one recurring occurrence. The owner's entry id is the only reference needed; no account is involved.</summary>
public sealed record RecurringTransactionEntryReference(long EntryId);