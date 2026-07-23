namespace FinanceManager.Application.Backfill;

/// <summary>
/// Structured outcome of one backfill run, sized for a single summary log line.
/// </summary>
/// <param name="TargetsInspected">Distinct targets (listings, currency pairs) the run looked at.</param>
/// <param name="ProviderRequests">External provider calls actually made (fresh targets are skipped).</param>
/// <param name="RowsInserted">New rows written to the database.</param>
/// <param name="RowsSkipped">Rows already present that were left untouched.</param>
/// <param name="Failures">Targets that failed (no symbol, provider error) without corrupting others.</param>
/// <param name="RateLimited">True when a provider quota/rate-limit stopped the run early; the next start resumes.</param>
public readonly record struct BackfillResult(
    int TargetsInspected,
    int ProviderRequests,
    int RowsInserted,
    int RowsSkipped,
    int Failures,
    bool RateLimited = false)
{
    /// <summary>A run that did nothing (e.g. the service is disabled by configuration).</summary>
    public static BackfillResult Empty => new(0, 0, 0, 0, 0);
}