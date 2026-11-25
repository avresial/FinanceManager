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

        var availableByName = availableLabels
            .GroupBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        // Attached labels can differ only by case (Food and FOOD), so each final name claims
        // an unused attached label, preferring an exact-case match, before falling back to
        // the user's available labels. This keeps both IDs when rules leave labels unchanged.
        var unclaimedAttached = entry.Labels.ToList();
        var resolvedLabels = result.FinalFacts.Labels
            .Select(name => ClaimAttached(unclaimedAttached, name) ?? availableByName.GetValueOrDefault(name))
            .OfType<FinancialLabel>()
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

    private static FinancialLabel? ClaimAttached(List<FinancialLabel> unclaimed, string name)
    {
        var label = unclaimed.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal))
            ?? unclaimed.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (label is not null)
            unclaimed.Remove(label);
        return label;
    }

    internal static TransactionFacts ToFacts(CurrencyAccountEntry entry) =>
        new(entry.ContractorDetails ?? string.Empty, entry.Description, entry.AccountId ?? throw new InvalidOperationException("A rule requires an account-linked entry."),
            Math.Abs(entry.ValueChange), entry.ValueChange switch
            {
                > 0 => TransactionDirection.Income,
                < 0 => TransactionDirection.Expense,
                _ => TransactionDirection.Transfer
            }, entry.Labels.Select(label => label.Name).ToList());
}