namespace FinanceManager.Application.Alerts.Models;

public sealed record AlertTransactionReference(
    int AccountId,
    int EntryId,
    DateTime PostingDate,
    decimal Amount,
    string Description,
    string? ContractorDetails);