using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Application.FinancialAccounts.Stock.Pricing;

public interface IAlphaVantageClient
{
    Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default);
    Task<IReadOnlyList<StockPrice>> GetDailySeries(string ticker, DateTime start, DateTime end, Currency currency, CancellationToken ct = default);
    Task<IReadOnlyList<StockPrice>> GetDailySeries(string ticker, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default);
}