using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
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

        var from = fromCurrency.Trim().ToUpperInvariant();
        var to = toCurrency.Trim().ToUpperInvariant();

        if (from == to)
            return 1m;

        if (_knownFactors.TryGetValue((from, to), out var knownFactor))
        {
            logger.LogDebug("Using known quote factor {From}→{To}: {Factor}", from, to, knownFactor);
            return knownFactor;
        }

        try
        {
            var fromCurr = new Currency { ShortName = from, Symbol = from };
            var toCurr = new Currency { ShortName = to, Symbol = to };
            var rate = await exchangeRateProvider.GetExchangeRateAsync(fromCurr, toCurr, DateTime.UtcNow);

            if (rate.HasValue && rate.Value > 0)
            {
                logger.LogDebug("Resolved exchange rate {From}→{To}: {Rate}", from, to, rate.Value);
                return rate.Value;
            }

            logger.LogWarning("Exchange rate service returned null/invalid for {From}→{To}; using 1.0", from, to);
            return 1m;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve exchange rate {From}→{To}; using 1.0", from, to);
            return 1m;
        }
    }
}