using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.TransactionRules;

namespace FinanceManager.Application.TransactionRules;

/// <summary>
/// A loaded, reusable transaction-rule application for one import or batch operation.
/// </summary>
public sealed class TransactionRuleApplication(
    IReadOnlyCollection<TransactionRule> rules,
    IReadOnlyCollection<FinancialLabel> availableLabels)
{
    public bool ApplyTo(CurrencyAccountEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return TransactionRuleService.ApplyToEntry(entry, rules, availableLabels);
    }
}