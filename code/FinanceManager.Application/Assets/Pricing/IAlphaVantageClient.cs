using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;

namespace FinanceManager.Application.Assets.Pricing;

public interface IAlphaVantageClient
{
    Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default);
    Task<IReadOnlyList<StockPrice>> GetDailySeries(string ticker, DateTime start, DateTime end, Currency currency, CancellationToken ct = default);
    Task<IReadOnlyList<StockPrice>> GetDailySeries(string ticker, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default);
}