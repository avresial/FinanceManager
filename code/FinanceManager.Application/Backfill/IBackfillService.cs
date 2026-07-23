namespace FinanceManager.Application.Backfill;

/// <summary>
/// One startup-backfill unit of work (stock prices, currency rates, …). Implementations are
/// resolved as a group and run sequentially by the startup orchestrator; sequential execution is
/// intentional because the underlying providers (Alpha Vantage free tier) are heavily rate-limited.
/// </summary>
public interface IBackfillService
{
    /// <summary>Short human-readable name used in structured logs (e.g. "StockPrices").</summary>
    string Name { get; }

    /// <summary>Deterministic run order; lower runs first. Ties break on <see cref="Name"/>.</summary>
    int Order { get; }

    /// <summary>
    /// Run the backfill once. Must be safe to call repeatedly (idempotent) and must not throw for
    /// per-target provider failures — those belong in <see cref="BackfillResult.Failures"/> so one
    /// bad target never aborts the rest of the run.
    /// </summary>
    Task<BackfillResult> BackfillAsync(CancellationToken cancellationToken);
}

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