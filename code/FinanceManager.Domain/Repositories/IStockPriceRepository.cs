using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Domain.Repositories;

public interface IStockPriceRepository
{
    Task<StockPrice> Add(string ticker, decimal pricePerUnit, Currency currency, DateTime date);
    Task<StockPrice> Update(string ticker, decimal pricePerUnit, Currency currency, DateTime date);
    Task<StockPrice?> Get(string ticker, DateTime date);
    Task<StockPrice?> GetThisOrNextOlder(string ticker, DateTime date);
    Task<DateTime?> GetLatestMissing(string ticker);
    Task<Currency?> GetTickerCurrency(string ticker);
}
