namespace FinanceManager.Domain.Transactions.Cash.Dtos;

/// <summary>A non-zero cash entry the recurring-payment detector analyses.</summary>
public sealed record RecurringCashCandidate(long EntryId, DateTime PostingDate, decimal ValueChange, string Description, string? ContractorDetails);