namespace FinanceManager.Domain.Assets.Discovery;

/// <summary>
/// A single listing-level result returned by external instrument discovery (OpenFIGI and/or
/// Alpha Vantage). Identity fields (FIGI/ISIN) are preferred from OpenFIGI; the provider symbol is
/// sourced from Alpha Vantage. This is intentionally distinct from
/// <see cref="FinanceManager.Domain.Assets.Dtos.InstrumentSearchResultDto"/>, which represents a
/// listing that already exists in the local database.
/// </summary>
public sealed class InstrumentDiscoveryResultDto
{
    /// <summary>Which provider(s) produced this result: <c>OpenFIGI</c>, <c>AlphaVantage</c>, or <c>Combined</c>.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Human-readable instrument name, e.g. "iShares Core S&amp;P 500 UCITS ETF USD (Acc)".</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Ticker for this listing, e.g. "CSPX", "SXR8", "AAPL".</summary>
    public string Ticker { get; init; } = string.Empty;

    /// <summary>ISO 10383 Market Identifier Code, when the exchange mapping is known, e.g. "XLON".</summary>
    public string? ExchangeMic { get; init; }

    /// <summary>Provider exchange code (e.g. the OpenFIGI exchange code "LN" or an Alpha Vantage region).</summary>
    public string? ExchangeCode { get; init; }

    /// <summary>Human-readable exchange name, when the mapping is known, e.g. "London Stock Exchange".</summary>
    public string? ExchangeName { get; init; }

    /// <summary>Currency this listing trades in, e.g. "USD", "EUR", "GBX".</summary>
    public string? TradingCurrency { get; init; }

    /// <summary>FIGI for this exact listing/venue, when available.</summary>
    public string? ListingFigi { get; init; }

    /// <summary>Share-class FIGI — the canonical identity key across venues.</summary>
    public string? ShareClassFigi { get; init; }

    /// <summary>Composite/country-level FIGI grouping a security's venues.</summary>
    public string? CompositeFigi { get; init; }

    /// <summary>ISIN, when available from the mapping/search context.</summary>
    public string? Isin { get; init; }

    /// <summary>Security type as reported by a provider (e.g. "ETF", "Equity").</summary>
    public string? SecurityType { get; init; }

    /// <summary>Market sector as reported by a provider, when available.</summary>
    public string? MarketSector { get; init; }

    /// <summary>Alpha Vantage (or other provider) symbol candidate used to fetch prices, e.g. "CSPX.LON".</summary>
    public string? ProviderSymbol { get; init; }

    /// <summary>Heuristic confidence in this result, 0..1. Higher when both providers corroborate the listing.</summary>
    public decimal ConfidenceScore { get; init; }

    /// <summary>User-facing warnings about ambiguous or incomplete data (exchange mapping, currency, missing symbol, ...).</summary>
    public List<string> Warnings { get; init; } = [];
}