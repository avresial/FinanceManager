using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Actions;

/// <summary>
/// Sets the transaction's labels: appends <see cref="Labels"/> to the working
/// label list (skipping blanks and labels already present), or replaces the whole
/// list when <see cref="ReplaceExisting"/> is set. A later rule therefore
/// overwrites an earlier rule's labels when it replaces them.
/// </summary>
public class SetLabelsAction : ITransactionRuleAction
{
    public SetLabelsAction(IReadOnlyCollection<string> labels, bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(labels);

        Labels = labels;
        ReplaceExisting = replaceExisting;
    }

    // Explicit constructor (not a primary constructor) so the parameterless
    // constructor can assign defaults independently of the validating overload.
    // Required for JSON deserialization.
    public SetLabelsAction()
    {
        Labels = [];
        ReplaceExisting = false;
    }

    public IReadOnlyCollection<string> Labels { get; init; }
    public bool ReplaceExisting { get; init; }

    public void Apply(TransactionFacts facts, List<string> workingLabels)
    {
        if (ReplaceExisting)
            workingLabels.Clear();

        foreach (var label in Labels)
        {
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var trimmed = label.Trim();
            if (!workingLabels.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                workingLabels.Add(trimmed);
        }
    }
}