using FinanceManager.Application.Assets.Pricing;
using FinanceManager.Application.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Application.Backfill;

/// <summary>
/// Startup stock-price backfill. Deliberately a thin adapter over the existing
/// <see cref="IInvestmentPriceBackfillService"/> so the discovery, rate-limited provider chain,
/// freshness check and idempotent upsert all stay in one place and are not duplicated here.
/// </summary>
public sealed class StockPriceBackfillService(
    IInvestmentPriceBackfillService backfillService,
    IOptions<BackfillOptions> options,
    ILogger<StockPriceBackfillService> logger) : IBackfillService
{
    public string Name => "StockPrices";

    public int Order => 10;

    public async Task<BackfillResult> BackfillAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.StockPricesEnabled)
        {
            logger.LogInformation("Stock-price backfill is disabled by configuration; skipping.");
            return BackfillResult.Empty;
        }

        var lookback = Math.Max(1, options.Value.StockLookbackDays);
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-lookback);

        // The wrapped service gathers every distinct held listing and runs each through
        // EnsureQuotesAsync, which already skips already-covered listings, calls the provider at
        // most once per listing, and upserts only missing dates — exactly the startup behaviour
        // required here. It reports coverage per listing rather than row-level insert/skip counts,
        // so those fields stay 0; inspected maps to listings and failures to uncovered listings.
        var result = await backfillService.BackfillAsync(start, end, cancellationToken);

        logger.LogInformation(
            "Stock-price backfill covered {Covered}/{Listings} listings over {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.",
            result.Covered, result.Listings, start, end);

        return new BackfillResult(
            TargetsInspected: result.Listings,
            ProviderRequests: 0,
            RowsInserted: 0,
            RowsSkipped: 0,
            Failures: result.Uncovered);
    }
}