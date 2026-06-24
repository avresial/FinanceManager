using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Application.FinancialAccounts.Stock.Pricing;

/// <summary>
/// Provider-agnostic source of daily stock prices. Implemented by each external price
/// provider (Alpha Vantage, EODHD, …) and by the fallback chain that composes them, so
/// <see cref="StockPriceProvider"/> can fetch prices without binding to a single vendor.
/// </summary>
public interface IStockPriceSource
{
    /// <summary>Short provider name used for logging and diagnostics (e.g. "AlphaVantage").</summary>
    string Name { get; }

    /// <summary>
    /// Fetches the daily close series for <paramref name="symbol"/> between
    /// <paramref name="start"/> and <paramref name="end"/> (inclusive). Returns an empty list
    /// when the provider has no data, is unconfigured, or fails — never throws for those cases,
    /// so a fallback source can take over.
    /// </summary>
    /// <param name="symbol">The provider-recognised symbol (e.g. "AAPL", "CSPX.LON").</param>
    /// <param name="isin">ISIN to stamp on the returned prices; the canonical key downstream.</param>
    Task<IReadOnlyList<StockPrice>> GetDailySeries(string symbol, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default);
}