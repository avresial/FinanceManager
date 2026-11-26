namespace FinanceManager.Application.Alerts.Models;

public sealed record AlertTransactionReference(
    long EntryId,
    DateTime PostingDate,
    decimal Amount,
    string Description,
    string? ContractorDetails,
    string? CurrencyCode = null);