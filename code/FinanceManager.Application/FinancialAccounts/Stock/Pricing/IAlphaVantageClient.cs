using FinanceManager.Application.FinancialAccounts.Stock.Market;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Application.FinancialAccounts.Stock.Pricing;

public interface IAlphaVantageClient
{
    Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default);
    Task<IReadOnlyList<StockPrice>> GetDailySeries(string ticker, DateTime start, DateTime end, Currency currency, CancellationToken ct = default);
    Task<IReadOnlyList<StockPrice>> GetDailySeries(string ticker, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default);
    Task<IReadOnlyList<StockListing>> GetListings(CancellationToken ct = default);
}