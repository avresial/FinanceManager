using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Assets.Pricing;

public class InvestmentPriceBackfillService(
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider,
    ILogger<InvestmentPriceBackfillService> logger) : IInvestmentPriceBackfillService
{
    public async Task<InvestmentPriceBackfillResult> BackfillAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var listingIds = await transactionRepository.GetDistinctAssetListingIds(ct);

        var covered = 0;
        var uncovered = 0;
        foreach (var listingId in listingIds)
        {
            ct.ThrowIfCancellationRequested();

            // Listings are processed sequentially on purpose: the provider chain is rate-limited
            // (Alpha Vantage free tier) and EnsureQuotesAsync serialises per-symbol fetches anyway,
            // so parallelism would only trip the limiter faster without finishing sooner.
            if (await priceProvider.EnsureQuotesAsync(listingId, start, end, ct))
                covered++;
            else
                uncovered++;
        }

        var result = new InvestmentPriceBackfillResult(listingIds.Count, covered, uncovered);
        logger.LogInformation(
            "Price backfill for {Start:yyyy-MM-dd}..{End:yyyy-MM-dd} finished: {Listings} listings, {Covered} covered, {Uncovered} without quotes.",
            start, end, result.Listings, result.Covered, result.Uncovered);
        return result;
    }
}