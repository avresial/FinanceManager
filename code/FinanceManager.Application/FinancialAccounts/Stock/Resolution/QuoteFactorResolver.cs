using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

public sealed class QuoteFactorResolver(
    ICurrencyExchangeRateProvider exchangeRateProvider,
    ILogger<QuoteFactorResolver> logger) : IQuoteFactorResolver
{
    // Conversion factors for currency pairs that share a base unit but differ in scale (not FX rates).
    // Contract: multiply a from-amount by the factor to get the to-amount.
    // 1 GBX (penny) = 0.01 GBP; 1 GBP = 100 GBX.
    private static readonly Dictionary<(string, string), decimal> _knownFactors = new()
    {
        { ("GBX", "GBP"), 0.01m },
        { ("GBP", "GBX"), 100m },
    };

    public async Task<decimal> ResolveAsync(string fromCurrency, string toCurrency, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fromCurrency) || string.IsNullOrWhiteSpace(toCurrency))
            return 1m;

        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return 1m;

        var key = (fromCurrency, toCurrency);
        if (_knownFactors.TryGetValue(key, out var knownFactor))
        {
            logger.LogDebug("Using known quote factor {From}→{To}: {Factor}", fromCurrency, toCurrency, knownFactor);
            return knownFactor;
        }

        try
        {
            var fromCurr = new Currency { ShortName = fromCurrency, Symbol = fromCurrency };
            var toCurr = new Currency { ShortName = toCurrency, Symbol = toCurrency };
            var rate = await exchangeRateProvider.GetExchangeRateAsync(fromCurr, toCurr, DateTime.UtcNow);

            if (rate.HasValue && rate.Value > 0)
            {
                logger.LogDebug("Resolved exchange rate {From}→{To}: {Rate}", fromCurrency, toCurrency, rate.Value);
                return rate.Value;
            }

            logger.LogWarning("Exchange rate service returned null/invalid for {From}→{To}; using 1.0", fromCurrency, toCurrency);
            return 1m;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve exchange rate {From}→{To}; using 1.0", fromCurrency, toCurrency);
            return 1m;
        }
    }
}