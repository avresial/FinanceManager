namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Resolves conversion factors between currency pairs.
/// Handles known cases (e.g., GBX ↔ GBP) efficiently, falls back to exchange rate API for others.
/// </summary>
public interface IQuoteFactorResolver
{
    /// <summary>
    /// Determine the conversion factor from one currency to another.
    /// </summary>
    /// <param name="fromCurrency">Source currency code (e.g., "GBX", "USD")</param>
    /// <param name="toCurrency">Target currency code (e.g., "GBP", "EUR")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Conversion factor (multiply from-amount by this to get to-amount). Returns 1.0 on failure.</returns>
    Task<decimal> ResolveAsync(string fromCurrency, string toCurrency, CancellationToken ct = default);
}