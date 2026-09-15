using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Actions;

/// <summary>
/// Replaces the user-visible description with <see cref="NormalizedValue"/>
/// (surrounding whitespace is trimmed when applied; an empty value clears it).
/// </summary>
public class NormalizeDescriptionAction : ITransactionRuleAction
{
    public NormalizeDescriptionAction(string normalizedValue)
    {
        ArgumentNullException.ThrowIfNull(normalizedValue);

        NormalizedValue = normalizedValue;
    }

    // Explicit constructor (not a primary constructor) so the parameterless
    // constructor can assign defaults independently of the validating overload.
    // Required for JSON deserialization.
    public NormalizeDescriptionAction()
    {
        NormalizedValue = string.Empty;
    }

    public string NormalizedValue { get; init; }

    public void Apply(TransactionFacts facts, List<string> labels) =>
        facts.Description = NormalizedValue.Trim();
}