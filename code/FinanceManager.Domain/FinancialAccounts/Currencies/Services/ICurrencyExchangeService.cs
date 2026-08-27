using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Services;

public interface ICurrencyExchangeService
{
    Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd);
    Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date);

    /// <summary>
    /// Resolves one exchange rate without discarding why a value is unavailable. In particular,
    /// <see cref="CurrencyExchangeRateStatus.NotYetPublished"/> tells callers where it is safe to
    /// request the current UTC date again; this method never retries that request itself.
    /// </summary>
    Task<CurrencyExchangeRateResult> GetExchangeRateResultAsync(Currency fromCurrency, Currency toCurrency, DateTime date);
}