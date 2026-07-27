namespace FinanceManager.Components.Shared.Models;

/// <summary>
/// Result of one stale-while-revalidate run. <paramref name="Model"/> is whatever the caller
/// should have on screen when the run finishes: the fresh model when it differed, the painted
/// snapshot when the fresh data matched or the request failed, and <c>null</c> when neither
/// fresh nor cached data was available.
/// </summary>
/// <param name="Outcome">How the refresh half of the run ended.</param>
/// <param name="Model">The model that should be displayed, or <c>null</c> when there is nothing to display.</param>
/// <param name="SnapshotPainted">Whether a stored snapshot was read and handed to the caller before the fresh request ran.</param>
/// <param name="Error">The exception thrown by the fresh request, when <paramref name="Outcome"/> is <see cref="SnapshotRefreshOutcome.Failed"/>.</param>
public sealed record SnapshotRefreshResult<TModel>(
    SnapshotRefreshOutcome Outcome,
    TModel? Model,
    bool SnapshotPainted,
    Exception? Error = null) where TModel : class
{
    /// <summary>
    /// <c>true</c> when the surface has nothing to show and the failure must be surfaced as a
    /// blocking error state. A failed refresh behind a painted snapshot is not blocking.
    /// </summary>
    public bool IsBlockingFailure => Outcome == SnapshotRefreshOutcome.Failed && !SnapshotPainted;
}