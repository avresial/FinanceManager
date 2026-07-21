namespace FinanceManager.Application.Assets.Pricing;

/// <summary>Outcome of one backfill run over the distinct held listings.</summary>
/// <param name="Listings">How many distinct listings the run covered.</param>
/// <param name="Covered">Listings that had at least one quote in the range after the run.</param>
/// <param name="Uncovered">Listings that still had no quote in the range (no symbol, provider empty/failed).</param>
public record InvestmentPriceBackfillResult(int Listings, int Covered, int Uncovered);

/// <summary>
/// Ensures stored end-of-day quotes exist for every investment listing anyone holds, over a given
/// date range. Designed to be triggered by an external scheduler (the weekly GitHub Actions cron)
/// so price history stays complete even when no user opens a chart.
/// </summary>
public interface IInvestmentPriceBackfillService
{
    /// <summary>Backfill quotes for all distinct held listings over [<paramref name="start"/>, <paramref name="end"/>].</summary>
    Task<InvestmentPriceBackfillResult> BackfillAsync(DateTime start, DateTime end, CancellationToken ct = default);
}