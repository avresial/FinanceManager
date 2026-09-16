using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Actions;

/// <summary>
/// An ordered transformation a rule applies to a matching transaction.
/// Actions mutate the engine's working label list and, when needed, the working
/// copy of the facts; they never touch the caller's input.
/// </summary>
public interface ITransactionRuleAction
{
    void Apply(List<string> labels, TransactionFacts? facts = null);
}