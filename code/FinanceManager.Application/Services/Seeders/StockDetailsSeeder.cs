using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class StockDetailsSeeder(
    IStockDetailsRepository stockDetailsRepository,
    IStockMarketService stockMarketService,
    ILogger<StockDetailsSeeder> logger) : ISeeder
{
    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var existingDetails = await stockDetailsRepository.GetAll(cancellationToken);

        if (existingDetails.Count > 2)
        {
            logger.LogInformation("Stock details already exist ({Count} records). Skipping seeding.", existingDetails.Count);
            return;
        }

        logger.LogInformation("No stock details found. Fetching listing status from API...");

        try
        {
            var stockListings = await stockMarketService.GetListingStatus(cancellationToken);

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
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed stock details from API.");
        }
    }
}