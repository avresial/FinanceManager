using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Services;

public interface ICurrencyExchangeRateProvider
{
    Task<CurrencyExchangeRateProviderResult> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date);
    Task<List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd);
}