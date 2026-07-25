using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Benchmarks.Hosting;

/// <summary>
/// A price source with no data, standing in for the Alpha Vantage → EODHD fallback chain.
/// </summary>
/// <remarks>
/// The seeded dataset already contains a full daily <c>PriceQuote</c> series, so the pricing chain
/// resolves every valuation from the repository and never reaches an external provider. This exists
/// only so a gap in the seeded series cannot silently turn a measured iteration into an HTTP call to
/// a rate-limited vendor. Returning an empty series is the interface's documented "no data" contract.
/// </remarks>
public sealed class OfflineStockPriceSource : IStockPriceSource
{
    public string Name => "Offline";

    public Task<IReadOnlyList<StockPrice>> GetDailySeries(string symbol, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StockPrice>>([]);
}