namespace FinanceManager.Api.Features.Insights.Services;

/// <summary>
/// The single definition of how long stored insights stay fresh. Both the read path that queues a
/// regeneration and the worker that decides whether to run one measure against this, so a user can
/// never be queued by one and permanently skipped by the other.
/// </summary>
public static class InsightsFreshness
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    /// <summary>
    /// Whether insights need regenerating, given when the newest stored one was created.
    /// A user with no insights at all counts as stale.
    /// </summary>
    public static bool IsStale(DateTime? newestCreatedAt, DateTime utcNow) =>
        newestCreatedAt is not DateTime createdAt || createdAt < utcNow - MaxAge;
}