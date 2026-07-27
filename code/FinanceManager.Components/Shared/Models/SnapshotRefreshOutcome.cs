namespace FinanceManager.Components.Shared.Models;

/// <summary>
/// How a stale-while-revalidate run ended. Describes the <em>refresh</em> half of the run;
/// whether a snapshot was painted first is reported separately by
/// <see cref="SnapshotRefreshResult{TModel}.SnapshotPainted"/>.
/// </summary>
public enum SnapshotRefreshOutcome
{
    /// <summary>Fresh data differed from what was painted; the UI and the stored snapshot were replaced.</summary>
    Refreshed,

    /// <summary>Fresh data matched the painted snapshot; nothing was repainted and nothing was written.</summary>
    Unchanged,

    /// <summary>The fresh request threw. A painted snapshot (if any) stays on screen.</summary>
    Failed,

    /// <summary>The fresh request succeeded but produced nothing to render.</summary>
    Empty,

    /// <summary>A newer run claimed the version gate while this one was in flight; it committed nothing.</summary>
    Superseded,
}