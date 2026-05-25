using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Domain.Services;

/// <summary>
/// Reads stock prices with automatic AlphaVantage fallback: tries the repository first,
/// fetches from the external API when data is missing, and persists newly fetched prices.
/// </summary>
public interface IStockPriceProvider
{
    /// <summary>
    /// Get price per unit for <paramref name="ticker"/> converted to <paramref name="targetCurrency"/> as of <paramref name="asOfDate"/>.
    /// Returns 0 when no price is available.
    /// </summary>
    Task<decimal> GetPricePerUnitAsync(string ticker, Currency targetCurrency, DateTime asOfDate);

    /// <summary>
    /// Get prices for <paramref name="ticker"/> over the given date range.
    /// Fetches from the external API and persists when local data is incomplete.
    /// </summary>
    Task<IReadOnlyList<StockPrice>> GetPricesAsync(string ticker, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// Get per-day price per unit for <paramref name="ticker"/> converted to <paramref name="targetCurrency"/>.
    /// Missing market days are filled from the latest known older price.
    /// </summary>
    Task<IReadOnlyDictionary<DateTime, decimal>> GetPricePerUnitSeriesAsync(
        string ticker,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);
}