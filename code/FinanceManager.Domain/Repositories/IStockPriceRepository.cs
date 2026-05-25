using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Domain.Repositories;

public interface IStockPriceRepository
{
    Task Add(IEnumerable<StockPrice> prices);
    Task<StockPrice> Add(string isin, decimal pricePerUnit, Currency currency, DateTime date);
    Task<StockPrice> Update(string isin, decimal pricePerUnit, Currency currency, DateTime date);
    Task<StockPrice?> Get(string isin, DateTime date);
    Task<IReadOnlyList<StockPrice>> GetRange(string isin, DateTime start, DateTime end);
    Task<StockPrice?> GetThisOrNextOlder(string isin, DateTime date);
    Task<DateTime?> GetLatestMissing(string isin);
    Task<Currency?> GetStockCurrency(string isin);
    Task<bool> Delete(int id, CancellationToken ct = default);
}