namespace FinanceManager.Application.Services.Ai;

public interface ILabelSetterAiService
{
    /// <summary>
    /// Given a list of entry IDs (for unlabelled currency account entries),
    /// asks the AI to assign existing label names to each entry.
    /// Returns a mapping of entryId → label name (only existing labels are returned).
    /// </summary>
    Task<Dictionary<int, string>> AssignLabels(
        IReadOnlyCollection<int> entryIds,
        CancellationToken cancellationToken = default);
}
