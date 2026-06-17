namespace FinanceManager.Domain.FinancialAccounts.Stock.Services;

/// <summary>
/// Resolves ticker symbols to ISIN (International Securities Identification Number).
/// ISIN is the canonical identifier for financial assets across regions.
/// </summary>
public interface IIsinResolver
{
    /// <summary>
    /// Resolves a ticker symbol to its ISIN.
    /// </summary>
    /// <param name="ticker">The ticker symbol (e.g., "AAPL").</param>
    /// <param name="region">Optional region/exchange code (e.g., "US" for NASDAQ/NYSE). Helps disambiguate when multiple exchanges list the same ticker.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ISIN string if found; null if unresolvable.</returns>
    Task<string?> ResolveAsync(string ticker, string? region = null, CancellationToken ct = default);
}