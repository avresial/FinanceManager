using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>Matches transactions whose <see cref="TransactionFacts.Direction"/> equals the configured direction.</summary>
public sealed record DirectionCondition(TransactionDirection Direction) : ITransactionRuleCondition
{
    public bool Matches(TransactionFacts facts) => facts.Direction == Direction;
}