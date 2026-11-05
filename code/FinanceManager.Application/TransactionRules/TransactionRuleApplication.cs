using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Models;

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
        var facts = ToFacts(entry);
        var result = TransactionRuleEngine.Run(rules, facts);
        if (!result.HasChanges) return false;

        var existingByName = entry.Labels
            .GroupBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var availableByName = availableLabels
            .GroupBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var resolvedLabels = result.FinalFacts.Labels
            .Where(name => existingByName.ContainsKey(name) || availableByName.ContainsKey(name))
            .Select(name => existingByName.GetValueOrDefault(name) ?? availableByName[name])
            .DistinctBy(label => label.Id)
            .ToList();
        var finalContractor = string.IsNullOrWhiteSpace(result.FinalFacts.Contractor) ? null : result.FinalFacts.Contractor;
        var contractorChanged = !string.Equals(entry.ContractorDetails, finalContractor, StringComparison.Ordinal);
        var descriptionChanged = !string.Equals(entry.Description, result.FinalFacts.Description, StringComparison.Ordinal);
        var labelsChanged = !entry.Labels.Select(label => label.Id).SequenceEqual(resolvedLabels.Select(label => label.Id));
        if (!contractorChanged && !descriptionChanged && !labelsChanged)
            return false;

        entry.ContractorDetails = finalContractor;
        entry.Description = result.FinalFacts.Description;
        entry.Labels = resolvedLabels;
        return true;
    }

    internal static TransactionFacts ToFacts(CurrencyAccountEntry entry) =>
        new(entry.ContractorDetails ?? string.Empty, entry.Description, entry.AccountId,
            Math.Abs(entry.ValueChange), entry.ValueChange switch
            {
                > 0 => TransactionDirection.Income,
                < 0 => TransactionDirection.Expense,
                _ => TransactionDirection.Transfer
            }, entry.Labels.Select(label => label.Name).ToList());
}