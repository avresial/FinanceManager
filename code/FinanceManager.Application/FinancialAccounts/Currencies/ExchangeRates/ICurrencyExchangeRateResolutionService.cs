using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

/// <summary>Resolves rates with their sources and accepts reusable daily values for range requests.</summary>
internal interface ICurrencyExchangeRateResolutionService
{
    Task<CurrencyExchangeRateResolution> GetExchangeRateWithSourceAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date);

    Task<List<(DateTime Date, decimal? Value, CurrencyExchangeRateSource Source)>> GetExchangeRateRangeWithProvenanceAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd,
        IReadOnlyDictionary<DateTime, CurrencyExchangeRateResolution>? cachedRates = null);
}