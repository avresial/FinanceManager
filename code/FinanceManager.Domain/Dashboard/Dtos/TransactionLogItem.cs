namespace FinanceManager.Domain.Dashboard.Dtos;

/// <summary>
/// One entry of the Transactions page feed backed by the full source entity, so the page can render it with the
/// account-specific row. The concrete type says which account kind the entry belongs to.
/// </summary>
public abstract record TransactionLogItem(int AccountId, string AccountName);