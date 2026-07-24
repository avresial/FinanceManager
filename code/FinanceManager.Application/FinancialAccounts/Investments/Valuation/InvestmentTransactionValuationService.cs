using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;

namespace FinanceManager.Application.FinancialAccounts.Investments.Valuation;

/// <summary>
/// Values individual Buy transactions for the transaction detail view. Mirrors the currency and
/// fallback conventions of <c>AssetsServiceInvestment.GetUnrealizedGainLossPerInstrument</c> but at
/// the granularity of a single transaction: the purchase-day value is converted at the trade-date
/// exchange rate, the current valuation at the latest rate, and when no rate is available the figures
/// stay in the transaction currency so amounts in different currencies are never mixed.
/// </summary>
internal class InvestmentTransactionValuationService(
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider,
    ICurrencyExchangeService currencyExchangeService) : IInvestmentTransactionValuationService
{
    public async Task<IReadOnlyList<InvestmentTransactionValuationDto>> GetForAccountAsync(
        int accountId,
        Currency targetCurrency,
        CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetByAccount(accountId, cancellationToken);

        var now = DateTime.UtcNow;
        // Memoise the two external lookups so an account with many trades in the same instrument
        // and currency issues one price call per (listing, display currency) and one FX call per
        // (currency, trade date) instead of one per transaction.
        var currentPriceByKey = new Dictionary<(long ListingId, string Currency), decimal>();
        var purchaseRateByKey = new Dictionary<(string Currency, DateOnly TradeDate), decimal?>();

        var results = new List<InvestmentTransactionValuationDto>();
        foreach (var transaction in transactions.Where(t => t.Type == InvestmentTransactionType.Buy))
        {
            var purchaseValueInTradeCurrency = transaction.Quantity * transaction.UnitPrice;
            var sameAsTarget = string.Equals(transaction.Currency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase);

            var purchaseRate = sameAsTarget
                ? 1m
                : await GetPurchaseRateAsync(transaction, targetCurrency, purchaseRateByKey);

            // Convert into the user's default currency only when the trade-date rate is known;
            // otherwise present the whole row in the transaction's own currency.
            var converted = purchaseRate is decimal rate && rate > 0m;
            var displayCurrency = converted ? targetCurrency : new Currency { ShortName = transaction.Currency, Symbol = transaction.Currency };

            var purchaseValue = converted ? purchaseValueInTradeCurrency * purchaseRate!.Value : purchaseValueInTradeCurrency;
            // Per-unit purchase price in the display currency, so it can be compared like-for-like
            // against the current unit price below.
            var purchaseUnitPrice = converted ? transaction.UnitPrice * purchaseRate!.Value : transaction.UnitPrice;

            // Current price is fetched already denominated in the display currency: the provider
            // applies the latest exchange rate for us, matching "latest rate for current valuation".
            var currentPrice = await GetCurrentPriceAsync(transaction.AssetListingId, displayCurrency, now, currentPriceByKey, cancellationToken);
            var hasCurrentPrice = currentPrice > 0m;
            var currentValuation = hasCurrentPrice ? transaction.Quantity * currentPrice : 0m;

            var gainLoss = hasCurrentPrice ? currentValuation - purchaseValue : 0m;
            var gainLossPercent = hasCurrentPrice && purchaseValue != 0m ? (currentValuation / purchaseValue - 1m) * 100m : 0m;

            results.Add(new InvestmentTransactionValuationDto(
                transaction.Id,
                purchaseUnitPrice,
                purchaseValue,
                currentPrice,
                currentValuation,
                gainLoss,
                gainLossPercent,
                displayCurrency.ShortName,
                converted,
                hasCurrentPrice));
        }

        return results;
    }

    private async Task<decimal?> GetPurchaseRateAsync(
        InvestmentTransaction transaction,
        Currency targetCurrency,
        Dictionary<(string Currency, DateOnly TradeDate), decimal?> cache)
    {
        var key = (transaction.Currency, transaction.TradeDate);
        if (cache.TryGetValue(key, out var cached)) return cached;

        var sourceCurrency = new Currency { ShortName = transaction.Currency, Symbol = transaction.Currency };
        var rate = await currencyExchangeService.GetExchangeRateAsync(
            sourceCurrency, targetCurrency, transaction.TradeDate.ToDateTime(TimeOnly.MinValue));
        cache[key] = rate;
        return rate;
    }

    private async Task<decimal> GetCurrentPriceAsync(
        long listingId,
        Currency displayCurrency,
        DateTime asOf,
        Dictionary<(long ListingId, string Currency), decimal> cache,
        CancellationToken cancellationToken)
    {
        var key = (listingId, displayCurrency.ShortName);
        if (cache.TryGetValue(key, out var cached)) return cached;

        var price = await priceProvider.GetPricePerUnitAsync(listingId, displayCurrency, asOf, cancellationToken);
        cache[key] = price;
        return price;
    }
}