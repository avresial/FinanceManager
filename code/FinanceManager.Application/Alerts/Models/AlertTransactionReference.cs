namespace FinanceManager.Application.Alerts.Models;

public sealed record AlertTransactionReference(
    int AccountId,
    long EntryId,
    DateTime PostingDate,
    decimal Amount,
    string Description,
    string? ContractorDetails);