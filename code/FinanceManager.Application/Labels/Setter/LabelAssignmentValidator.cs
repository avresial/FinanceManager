using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Labels.Setter;

internal sealed class LabelAssignmentValidator(ILogger<LabelAssignmentValidator> logger)
{
    public Dictionary<int, string> Validate(
        IReadOnlyCollection<LabelAssignment> assignments,
        IReadOnlyCollection<int> requestedEntryIds,
        IReadOnlySet<string> knownLabelNames)
    {
        var result = new Dictionary<int, string>();
        var entryIdSet = new HashSet<int>(requestedEntryIds);
        var hasNoMatchSentinel = knownLabelNames.Contains(WellKnownFinancialLabels.NoMatch);

        foreach (var assignment in assignments)
        {
            if (assignment.EntryId is null) continue;
            if (!entryIdSet.Contains(assignment.EntryId.Value)) continue;

            // AI returned null / empty / unknown label → use the NoMatch sentinel so the entry
            // still gets a label and won't be re-queued on the next startup scan.
            var labelName = assignment.LabelName;
            if (string.IsNullOrWhiteSpace(labelName) || !knownLabelNames.Contains(labelName))
            {
                if (!hasNoMatchSentinel)
                {
                    logger.LogWarning(
                        "AI returned no fitting label for entry {EntryId} and the '{Sentinel}' label is missing — entry will stay unlabelled and be re-queued.",
                        assignment.EntryId.Value,
                        WellKnownFinancialLabels.NoMatch);
                    continue;
                }
                labelName = WellKnownFinancialLabels.NoMatch;
            }

            result[assignment.EntryId.Value] = labelName;
        }

        return result;
    }
}