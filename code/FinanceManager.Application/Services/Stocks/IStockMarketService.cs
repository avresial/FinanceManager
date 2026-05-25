using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Application.Services.Stocks;

public interface IStockMarketService
{
    Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default);
    Task<IReadOnlyList<StockPrice>> GetStockPrices(string ticker, DateTime start, DateTime end, CancellationToken ct = default);
    Task<IReadOnlyList<StockDetails>> ListStockDetails(CancellationToken ct = default);
}