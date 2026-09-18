using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Services;

/// <summary>
/// Application-level contract for running transaction automation rules. The engine
/// itself (<see cref="FinanceManager.Domain.TransactionRules.TransactionRuleEngine"/>)
/// is a pure, deterministic operation; this contract is the seam for the import and
/// transaction-edit use cases to depend on.
/// </summary>
public interface ITransactionRuleEngineService
{
    /// <summary>
    /// Runs the given rules against the given transaction facts. The facts are never
    /// mutated; the returned result carries the final values and the per-rule trail.
    /// </summary>
    TransactionRuleEngineResult RunRules(IEnumerable<TransactionRule> rules, TransactionFacts facts);
}