using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Services;

public interface ICurrencyExchangeRateProvider
{
    Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date);
    Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd);
}