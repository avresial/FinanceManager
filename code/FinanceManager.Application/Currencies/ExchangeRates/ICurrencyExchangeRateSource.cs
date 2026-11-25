using FinanceManager.Domain.Currencies.Entities;

namespace FinanceManager.Application.Currencies.ExchangeRates;

/// <summary>
/// Resolves exact daily rates for a direct currency pair from one layer of the source chain.
/// Dates that cannot be attempted within the layer's budget are omitted from the result.
/// </summary>
public interface ICurrencyExchangeRateSource
{
    Task<IReadOnlyDictionary<DateTime, CurrencyExchangeRateResolution>> ResolveAsync(
        Currency fromCurrency,
        Currency toCurrency,
        IReadOnlyCollection<DateTime> dates,
        bool reuseCachedMisses,
        CancellationToken ct = default,
        CurrencyExchangeRateResolutionContext? context = null);
}