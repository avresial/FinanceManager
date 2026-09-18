namespace FinanceManager.Domain.TransactionRules.Commands;

public sealed record ApplyTransactionRules(
    bool Confirmed,
    int? AccountId = null,
    int? EntryId = null);