namespace FinanceManager.Application.Labels.Setter;

public interface ILabelSetterAiService
{
    /// <summary>
    /// Given a list of the owner's entry IDs (for unlabelled cash entries),
    /// asks the AI to assign existing label names to each entry. Entries of other users are never read.
    /// Returns a mapping of entryId → label name (only existing labels are returned).
    /// </summary>
    Task<Dictionary<long, string>> AssignLabels(
        int userId,
        IReadOnlyCollection<long> entryIds,
        CancellationToken cancellationToken = default);
}