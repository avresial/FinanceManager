using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Services;

public interface ICurrencyExchangeRateProvider
{
    Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date);
    Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd);
}