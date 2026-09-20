namespace FinanceManager.Domain.TransactionRules.Commands;

public sealed record ReorderTransactionRules(IReadOnlyList<Guid> RuleIds);