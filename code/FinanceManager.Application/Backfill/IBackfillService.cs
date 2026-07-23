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