using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Domain.Repositories;

public interface IStockPriceRepository
{
    Task Add(IEnumerable<StockPrice> prices);
    Task<StockPrice> Add(string ticker, decimal pricePerUnit, Currency currency, DateTime date);
    Task<StockPrice> Update(string ticker, decimal pricePerUnit, Currency currency, DateTime date);
    Task<StockPrice?> Get(string ticker, DateTime date);
    Task<IReadOnlyList<StockPrice>> GetRange(string ticker, DateTime start, DateTime end);
    Task<StockPrice?> GetThisOrNextOlder(string ticker, DateTime date);
    Task<DateTime?> GetLatestMissing(string ticker);
    Task<Currency?> GetTickerCurrency(string ticker);
    Task<bool> Delete(int id, CancellationToken ct = default);
}