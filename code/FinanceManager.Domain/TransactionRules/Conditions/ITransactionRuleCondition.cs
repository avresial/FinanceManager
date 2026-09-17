using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>A predicate one of a rule's conditions must satisfy for the rule to match.</summary>
public interface ITransactionRuleCondition
{
    /// <summary>Returns true when the condition is satisfied by the given facts.</summary>
    bool Matches(TransactionFacts facts);
}