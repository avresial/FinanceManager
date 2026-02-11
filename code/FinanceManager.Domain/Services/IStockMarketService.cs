using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Domain.Services;

public interface IStockMarketService
{
    Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default);
    Task<IReadOnlyList<StockPrice>> GetDailyStock(string ticker, DateTime start, DateTime end, CancellationToken ct = default);
}
