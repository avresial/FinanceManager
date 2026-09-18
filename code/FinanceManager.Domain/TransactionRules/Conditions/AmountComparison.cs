namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>The comparison applied between a transaction amount and an <see cref="AmountCondition"/>'s threshold.</summary>
public enum AmountComparison
{
    LessThan,
    LessThanOrEqual,
    Equal,
    GreaterThanOrEqual,
    GreaterThan,
}