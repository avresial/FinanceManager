using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Actions;

/// <summary>
/// An ordered transformation a rule applies to a matching transaction.
/// Actions mutate the engine's working copy of the facts and its working label
/// list; they never touch the caller's input.
/// </summary>
public interface ITransactionRuleAction
{
    void Apply(TransactionFacts facts, List<string> labels);
}