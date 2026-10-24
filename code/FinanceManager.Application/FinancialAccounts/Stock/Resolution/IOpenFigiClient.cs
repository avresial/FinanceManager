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
/// <param name="Isin">
/// ISIN cross-reference. OpenFIGI never echoes ISIN on a ticker mapping, so this is <c>null</c> for
/// ticker lookups; it is only populated when the instrument was looked up by ISIN (the
/// <c>ID_ISIN</c> input path stamps the queried value back onto its results).
/// </param>
/// <param name="Figi">Venue-level FIGI (the specific listing on this exchange).</param>
/// <param name="CompositeFigi">Country/composite-level FIGI grouping a security's venues.</param>
/// <param name="ShareClassFigi">
/// Share-class FIGI — the canonical identity key for the security across venues. OpenFIGI returns
/// this on every mapping; the resolver keys identity and auto-resolution on it.
/// </param>
/// <param name="SecurityType">OpenFIGI <c>securityType</c>, e.g. <c>ETP</c> or <c>Common Stock</c>.</param>
/// <param name="SecurityType2">OpenFIGI <c>securityType2</c>, e.g. <c>Mutual Fund</c>.</param>
/// <param name="MarketSector">OpenFIGI <c>marketSector</c>, e.g. <c>Equity</c>.</param>
/// <remarks>
/// OpenFIGI's mapping response has no currency field, so no trading currency is carried here; the
/// discovery service derives it from a correlated Alpha Vantage match or the venue's default.
/// </remarks>
public sealed record OpenFigiListing(
    string? Isin,
    string Ticker,
    string Name,
    string ExchCode,
    string? Figi = null,
    string? CompositeFigi = null,
    string? ShareClassFigi = null,
    string? SecurityType = null,
    string? SecurityType2 = null,
    string? MarketSector = null);