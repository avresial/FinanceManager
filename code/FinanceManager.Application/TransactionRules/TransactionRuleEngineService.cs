using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Models;
using FinanceManager.Domain.TransactionRules.Services;

namespace FinanceManager.Application.TransactionRules;

/// <summary>
/// Default <see cref="ITransactionRuleEngineService"/>: delegates to the pure
/// domain engine. The service is stateless, so the result for a given rule set
/// and transaction is always identical, which keeps import previews and the
/// applied result consistent.
/// </summary>
public class TransactionRuleEngineService : ITransactionRuleEngineService
{
    public TransactionRuleEngineResult RunRules(IEnumerable<TransactionRule> rules, TransactionFacts facts)
        => TransactionRuleEngine.Run(rules, facts);
}