using FinanceManager.Application.FinancialAccounts.Stock.Market;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

internal class DataBackfillService(
    AppDbContext context,
    IIsinResolver isinResolver,
    ILogger<DataBackfillService> logger) : IDataBackfillService
{
    public async Task<int> BackfillStockDetailsIsinAsync(CancellationToken ct = default)
    {
        var stockDetailsToBackfill = await context.StockDetails
            .Where(sd => sd.Isin == null || sd.Isin == "")
            .ToListAsync(ct);

        int successCount = 0;
        foreach (var stockDetails in stockDetailsToBackfill)
        {
            try
            {
                var isin = await isinResolver.ResolveAsync(stockDetails.Ticker, ct: ct);
                if (!string.IsNullOrWhiteSpace(isin))
                {
                    stockDetails.Isin = isin;
                    successCount++;
                }
                else
                {
                    logger.LogWarning("Could not resolve ISIN for ticker {Ticker}", stockDetails.Ticker);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error backfilling ISIN for ticker {Ticker}", stockDetails.Ticker);
            }
        }

        if (successCount > 0)
            await context.SaveChangesAsync(ct);

        logger.LogInformation("Backfilled ISIN for {Count}/{Total} StockDetails records", successCount, stockDetailsToBackfill.Count);
        return successCount;
    }

    public async Task<int> BackfillStockEntriesIsinAsync(CancellationToken ct = default)
    {
        // Join StockEntries with StockDetails to backfill ISIN
        var entriesToUpdate = await context.StockEntries
            .Where(se => se.Isin == null || se.Isin == "")
            .Join(context.StockDetails,
                se => se.Ticker,
                sd => sd.Ticker,
                (se, sd) => new { Entry = se, StockDetails = sd })
            .Where(x => x.StockDetails.Isin != null && x.StockDetails.Isin != "")
            .Select(x => x.Entry)
            .ToListAsync(ct);

        int successCount = 0;
        foreach (var entry in entriesToUpdate)
        {
            try
            {
                var stockDetails = await context.StockDetails
                    .FirstOrDefaultAsync(sd => sd.Ticker == entry.Ticker, ct);

                if (stockDetails?.Isin is not null)
                {
                    entry.Isin = stockDetails.Isin;
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error backfilling ISIN for StockEntry {EntryId}", entry.EntryId);
            }
        }

        if (successCount > 0)
            await context.SaveChangesAsync(ct);

        logger.LogInformation("Backfilled ISIN for {Count}/{Total} StockEntries records", successCount, entriesToUpdate.Count);
        return successCount;
    }
}