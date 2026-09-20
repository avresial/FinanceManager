using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>
/// Matches the transaction amount against a threshold, scoped to a single
/// <see cref="TransactionDirection"/> so "expenses over 50" and "income over 50"
/// are unambiguous. The condition matches when the facts' direction equals the
/// condition's direction and the (non-negative) amount satisfies the comparison.
/// </summary>
public sealed record AmountCondition(
    TransactionDirection Direction,
    decimal Threshold,
    AmountComparison Comparison = AmountComparison.GreaterThan) : ITransactionRuleCondition
{
    public bool Matches(TransactionFacts facts)
    {
        if (facts.Direction != Direction)
            return false;

        return Comparison switch
        {
            AmountComparison.LessThan => facts.Amount < Threshold,
            AmountComparison.LessThanOrEqual => facts.Amount <= Threshold,
            AmountComparison.Equal => facts.Amount == Threshold,
            AmountComparison.GreaterThanOrEqual => facts.Amount >= Threshold,
            AmountComparison.GreaterThan => facts.Amount > Threshold,
            _ => false,
        };
    }
}