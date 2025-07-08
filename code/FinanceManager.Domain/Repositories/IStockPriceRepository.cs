using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Repositories;

public interface IStockPriceRepository
{
    Task<StockPrice> AddStockPrice(string ticker, decimal pricePerUnit, string currency, DateTime date);
    Task<StockPrice> UpdateStockPrice(string ticker, decimal pricePerUnit, string currency, DateTime date);
    Task<StockPrice?> GetStockPrice(string ticker, DateTime Date);
    Task<DateTime?> GetLatestMissingStockPrice(string ticker);
    Task<string?> GetTickerCurrency(string ticker);
}
