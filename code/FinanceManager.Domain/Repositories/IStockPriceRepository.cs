using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Repositories;

public interface IStockPriceRepository
{
    Task<StockPrice> Add(string ticker, decimal pricePerUnit, string currency, DateTime date);
    Task<StockPrice> Update(string ticker, decimal pricePerUnit, string currency, DateTime date);
    Task<StockPrice?> Get(string ticker, DateTime date);
    Task<StockPrice?> GetThisOrNextOlder(string ticker, DateTime date);
    Task<DateTime?> GetLatestMissing(string ticker);
    Task<string?> GetTickerCurrency(string ticker);
}
