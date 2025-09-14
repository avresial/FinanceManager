using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Services;
public interface ICurrencyExchangeService
{
    Task<decimal?> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime date);
    Task<decimal> GetPricePerUnit(StockPrice stockPrice, string currency, DateTime date);
}
