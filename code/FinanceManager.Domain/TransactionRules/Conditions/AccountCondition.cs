using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>
/// Matches the account the transaction belongs to. The condition matches when the
/// facts' account id is one of the configured <see cref="AccountIds"/>.
/// </summary>
public sealed record AccountCondition(IReadOnlyCollection<int> AccountIds) : ITransactionRuleCondition
{
    private readonly HashSet<int> _accountIds = [.. AccountIds];

    public bool Matches(TransactionFacts facts) => _accountIds.Contains(facts.AccountId);
}