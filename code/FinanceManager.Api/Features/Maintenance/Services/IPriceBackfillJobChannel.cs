namespace FinanceManager.Api.Features.Maintenance.Services;

/// <summary>Date range a queued backfill run should cover (inclusive, UTC dates).</summary>
public record PriceBackfillJobRequest(DateTime Start, DateTime End);

public interface IPriceBackfillJobChannel
{
    /// <summary>
    /// Queue a backfill run without blocking. Returns <c>false</c> when a run is already queued —
    /// duplicate triggers (a retried cron request) collapse into the pending run instead of piling up.
    /// </summary>
    bool TryQueueJob(PriceBackfillJobRequest request);

    IAsyncEnumerable<PriceBackfillJobRequest> ReadAll(CancellationToken cancellationToken);
}