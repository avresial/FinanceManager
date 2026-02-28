using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Application.Services.Stocks;

public interface IAlphaVantageClient
{
    Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default);
    Task<IReadOnlyList<StockPrice>> GetDailySeries(string ticker, DateTime start, DateTime end, Currency currency, CancellationToken ct = default);
    Task<IReadOnlyList<StockListing>> GetListings(CancellationToken ct = default);
}