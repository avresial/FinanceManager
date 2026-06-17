using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Domain.Services;

public interface ICurrencyExchangeService
{
    Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd);
    Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date);
    Task<decimal?> GetPricePerUnit(StockPrice stockPrice, Currency currency, DateTime date);
}