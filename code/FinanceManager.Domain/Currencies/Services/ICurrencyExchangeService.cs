using FinanceManager.Domain.Currencies.Entities;

namespace FinanceManager.Domain.Currencies.Services;

public interface ICurrencyExchangeService
{
    Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd);
    Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd, CancellationToken cancellationToken);
    Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date);
    Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves one exchange rate without discarding why a value is unavailable. In particular,
    /// <see cref="CurrencyExchangeRateResult.NotYetPublished"/> tells callers where it is safe to
    /// request the current UTC date again; this method never retries that request itself.
    /// </summary>
    Task<CurrencyExchangeRateResult> GetExchangeRateResultAsync(Currency fromCurrency, Currency toCurrency, DateTime date);
}