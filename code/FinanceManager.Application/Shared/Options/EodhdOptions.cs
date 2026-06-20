namespace FinanceManager.Application.Shared.Options;

/// <summary>
/// Configuration for the EODHD (eodhd.com) price provider, used as the fallback price source
/// behind Alpha Vantage. EODHD exposes split/dividend-adjusted end-of-day history (adjusted_close)
/// on its free tier, so it covers cases where Alpha Vantage is rate-limited or lacks entitlement.
/// </summary>
public sealed class EodhdOptions
{
    /// <summary>Base URL for the EODHD API. Defaults to the public endpoint.</summary>
    public string BaseUrl { get; set; } = "https://eodhd.com/api";

    /// <summary>
    /// EODHD API token. Empty by default — supply via User Secrets or environment variable,
    /// never commit it. Without a token the client returns no prices (logged as a warning).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}