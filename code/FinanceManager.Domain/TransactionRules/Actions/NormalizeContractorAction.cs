using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Actions;

/// <summary>
/// Replaces the user-visible contractor/merchant text with <see cref="NormalizedValue"/>
/// (surrounding whitespace is trimmed when applied; an empty value clears it).
/// </summary>
public class NormalizeContractorAction : ITransactionRuleAction
{
    public NormalizeContractorAction(string normalizedValue)
    {
        ArgumentNullException.ThrowIfNull(normalizedValue);

        NormalizedValue = normalizedValue;
    }

    // Explicit constructor (not a primary constructor) so the parameterless
    // constructor can assign defaults independently of the validating overload.
    // Required for JSON deserialization.
    public NormalizeContractorAction()
    {
        NormalizedValue = string.Empty;
    }

    public string NormalizedValue { get; init; }

    public void Apply(TransactionFacts facts, List<string> labels) =>
        facts.Contractor = NormalizedValue.Trim();
}