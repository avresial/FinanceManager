using FinanceManager.Application.FinancialAccounts.Stock.Market;
using FinanceManager.Application.Options;
using FinanceManager.Application.Services.Seeders;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Application.FinancialAccounts.Stock.Seeders;

public class StockDetailsSeeder(
    IStockDetailsRepository stockDetailsRepository,
    IStockMarketService stockMarketService,
    IOptions<StockDetailsSeederOptions> options,
    ILogger<StockDetailsSeeder> logger) : ISeeder
{
    public async Task Seed(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Stock details seeding is disabled. Set {SectionName}:Enabled=true to run the startup import.",
                StockDetailsSeederOptions.SectionName);
            return;
        }

        var existingDetails = await stockDetailsRepository.GetAll(cancellationToken);

        if (existingDetails.Count > 2)
        {
            logger.LogInformation("Stock details already exist ({Count} records). Skipping seeding.", existingDetails.Count);
            return;
        }

        logger.LogInformation("No stock details found. Fetching listing status from API...");

        try
        {
            var stockListings = await stockMarketService.ListStockDetails(cancellationToken);

            if (stockListings.Count == 0)
            {
                logger.LogWarning("No stock listings retrieved from API. Seeding aborted.");
                return;
            }

            logger.LogInformation("Retrieved {Count} stock listings from API. Starting to seed...", stockListings.Count);

            var seededCount = 0;
            foreach (var stockDetails in stockListings)
            {
                try
                {
                    await stockDetailsRepository.Add(stockDetails, cancellationToken);
                    seededCount++;

                    if (seededCount % 1000 == 0)
                        logger.LogInformation("Seeded {Count}/{Total} stock details...", seededCount, stockListings.Count);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to seed stock detail for ticker {Ticker}. Continuing...", stockDetails.Ticker);
                }
            }

            logger.LogInformation("StockDetails seeding completed. Successfully added {Count} out of {Total} stocks.",
                seededCount, stockListings.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Stock details seeding was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed stock details from API.");
        }
    }
}