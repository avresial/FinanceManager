using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>
/// Matches the account the transaction belongs to. The condition matches when the
/// facts' account id is one of the configured <see cref="AccountIds"/>. An entry without a legacy
/// account link never matches.
/// </summary>
public sealed record AccountCondition(IReadOnlyCollection<int> AccountIds) : ITransactionRuleCondition
{
    private readonly HashSet<int> _accountIds = [.. AccountIds];

    public bool Matches(TransactionFacts facts) => facts.AccountId is int accountId && _accountIds.Contains(accountId);
}