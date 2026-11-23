namespace FinanceManager.Domain.Transactions.Cash.Dtos;

/// <summary>
/// A non-zero cash entry the recurring-payment detector analyses. <paramref name="AccountId"/> is null for entries that are not
/// linked to an account.
/// </summary>
public sealed record RecurringCashCandidate(long EntryId, int? AccountId, DateTime PostingDate, decimal ValueChange, string Description, string? ContractorDetails);