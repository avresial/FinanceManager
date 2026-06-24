using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Services;

public interface ICurrencyExchangeService
{
    Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd);
    Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date);
}