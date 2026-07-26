using FinanceManager.Components.Shared.Services;
namespace FinanceManager.Components.Shared.Models;

/// <summary>
/// Base type for any UI state that can be persisted through <c>ISnapshotService</c>.
/// A snapshot captures the last-rendered view of a surface so it can be re-shown
/// instantly on the next visit, then reconciled against freshly fetched data.
/// </summary>
public abstract class SnapshotBase
{
    /// <summary>When the snapshot was captured. Metadata only — not part of the rendered content.</summary>
    public DateTime FetchedAtUtc { get; set; } = DateTime.UtcNow;
}