using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Services;
public interface ICurrencyExchangeService
{
    Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date);
    Task<decimal> GetPricePerUnit(StockPrice stockPrice, Currency currency, DateTime date);
}
