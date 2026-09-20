namespace FinanceManager.Domain.TransactionRules.Models;

/// <summary>
/// The sign semantics of a transaction amount. Rules and their amount conditions
/// always match direction explicitly, so an amount of 50 means "50 in" for Income
/// and "50 out" for Expense rather than relying on signed values.
/// </summary>
public enum TransactionDirection
{
    Income,
    Expense,
    Transfer,
}