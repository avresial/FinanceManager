namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// OpenFIGI API client abstraction.
/// Resolves base tickers + exchange codes and ISINs to metadata and listing information.
/// </summary>
public interface IOpenFigiClient
{
    /// <summary>
    /// Maps a base ticker and optional exchange code to OpenFIGI listing metadata.
    /// </summary>
    Task<IReadOnlyList<OpenFigiListing>> MapByTickerAsync(string baseTicker, string? exchCode = null, CancellationToken ct = default);

    /// <summary>
    /// Maps an ISIN to all known listing venues and metadata.
    /// </summary>
    Task<IReadOnlyList<OpenFigiListing>> MapByIsinAsync(string isin, CancellationToken ct = default);
}

/// <summary>
/// A single listing result from OpenFIGI, representing one venue for an instrument.
/// </summary>
public sealed record OpenFigiListing(
    string? Isin,
    string Ticker,
    string Name,
    string ExchCode,
    string? Currency);