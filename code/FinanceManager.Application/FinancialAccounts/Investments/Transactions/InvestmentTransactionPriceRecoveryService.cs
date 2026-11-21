using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Investments.Transactions;

/// <summary>
/// Recovers and saves missing unit prices before the caller returns the transactions.
/// </summary>
public sealed class InvestmentTransactionPriceRecoveryService(
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider,
    ICacheInvalidator dashboardCacheInvalidator,
    ILogger<InvestmentTransactionPriceRecoveryService> logger)
{
    /// <summary>
    /// Recovers missing prices on <paramref name="transactions"/> in place. A trade keeps its previous
    /// price when the repository reports that the update was not saved.
    /// </summary>
    public async Task RecoverMissingPricesAsync(
        IReadOnlyList<InvestmentTransaction> transactions,
        int userId,
        CancellationToken cancellationToken)
    {
        var recoveredAny = false;
        var missingPrices = transactions
            .Where(x => x.UnitPrice <= 0m && !string.IsNullOrWhiteSpace(x.Currency))
            .GroupBy(x => new
            {
                x.AssetListingId,
                x.Currency,
                x.TradeDate
            });

        foreach (var group in missingPrices)
        {
            decimal price;
            try
            {
                var currency = new Currency(0, group.Key.Currency, group.Key.Currency);
                price = await priceProvider.GetPricePerUnitAsync(
                    group.Key.AssetListingId,
                    currency,
                    group.Key.TradeDate.ToDateTime(TimeOnly.MinValue),
                    cancellationToken);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Price recovery cancelled for listing {ListingId} on {TradeDate}.", group.Key.AssetListingId, group.Key.TradeDate);
                throw;
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Price recovery cancelled or timed out for listing {ListingId} on {TradeDate}; continuing.", group.Key.AssetListingId, group.Key.TradeDate);
                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to recover price for listing {ListingId} on {TradeDate}", group.Key.AssetListingId, group.Key.TradeDate);
                continue;
            }

            if (price <= 0m) continue;

            // ponytail: one save per recovered trade; batch updates only if accounts accumulate a large backlog.
            foreach (var transaction in group)
            {
                var previousPrice = transaction.UnitPrice;
                transaction.UnitPrice = price;
                if (await transactionRepository.Update(transaction, cancellationToken))
                    recoveredAny = true;
                else
                    transaction.UnitPrice = previousPrice;
            }
        }

        if (recoveredAny)
            await dashboardCacheInvalidator.InvalidateUser(userId);
    }
}