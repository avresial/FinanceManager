namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Normalizes a broker ticker (e.g., "CSPX.UK") into base ticker + exchange hint.
/// Maintains a lookup table of broker suffixes to OpenFIGI exchange codes and Alpha Vantage regions.
/// </summary>
public sealed class BrokerSymbol
{
    public required string BaseTicker { get; set; }
    public string? ExchangeHint { get; set; }

    // Broker suffix → (OpenFIGI exchange code, ISO 10383 MIC, exchange name, Alpha Vantage region aliases,
    // default trading currency, OpenFIGI country-composite codes).
    // Alpha Vantage's SYMBOL_SEARCH "region" is a free-form label ("United Kingdom", not "GB"),
    // so each suffix carries the aliases we accept when correlating an AV match to the hint.
    // DefaultCurrency fills the gap left by OpenFIGI (which never returns a currency); it is null
    // where a venue legitimately trades several currencies (LSE: GBX/GBP/USD).
    // CompositeOpenFigiCodes are OpenFIGI codes that group a country's venues (e.g. "GR" for
    // Germany). They resolve to this venue only as a fallback when no primary listing exists.
    private static readonly Dictionary<string, ExchangeMapping> _suffixMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "UK", new("LN", "XLON", "London Stock Exchange", ["United Kingdom", "GB", "UK", "London"], null, []) },
        { "DE", new("GY", "XETR", "Xetra", ["Germany", "XETRA", "DE"], "EUR", ["GR"]) },
        { "PL", new("WA", "XWAR", "Warsaw Stock Exchange", ["Poland", "Warsaw", "PL"], "PLN", []) },
        // OpenFIGI "US" is a True Composite covering all US venues (Nasdaq, NYSE, ARCA, ...), so no
        // single ISO 10383 MIC applies — leave the MIC unset rather than mis-attributing to one.
        { "US", new("US", null, "United States", ["United States", "US", "USA"], "USD", []) },
        { "CA", new("CT", "XTSE", "Toronto Stock Exchange", ["Canada", "Toronto", "CA"], "CAD", []) },
        { "AU", new("AU", "XASX", "Australian Securities Exchange", ["Australia", "AU"], "AUD", []) },
        { "JP", new("TT", "XTKS", "Tokyo Stock Exchange", ["Japan", "Tokyo", "JP"], "JPY", []) },
        { "HK", new("HK", "XHKG", "Hong Kong Stock Exchange", ["Hong Kong", "HK"], "HKD", []) },
        { "SG", new("SI", "XSES", "Singapore Exchange", ["Singapore", "SG"], "SGD", []) },
        { "CH", new("VX", "XSWX", "SIX Swiss Exchange", ["Switzerland", "Swiss", "CH"], "CHF", []) },
    };

    public static BrokerSymbol Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new BrokerSymbol { BaseTicker = string.Empty };

        var normalized = input.Trim().ToUpperInvariant();
        var lastDot = normalized.LastIndexOf('.');

        // Only treat the trailing segment as an exchange hint when it is a known broker suffix.
        // This preserves legitimately dotted tickers such as "BRK.B".
        if (lastDot > 0 && lastDot < normalized.Length - 1)
        {
            var hintCandidate = normalized[(lastDot + 1)..];
            if (_suffixMap.ContainsKey(hintCandidate))
            {
                return new BrokerSymbol
                {
                    BaseTicker = normalized[..lastDot],
                    ExchangeHint = hintCandidate
                };
            }
        }

        return new BrokerSymbol { BaseTicker = normalized };
    }

    public ExchangeMapping? TryLookupExchange()
    {
        if (string.IsNullOrWhiteSpace(ExchangeHint))
            return null;

        return _suffixMap.TryGetValue(ExchangeHint, out var mapping) ? mapping : null;
    }

    /// <summary>
    /// Resolve a venue by any known token — a broker suffix ("UK"), an OpenFIGI exchange code
    /// ("LN"), or an Alpha Vantage region label ("GB"/"United Kingdom") — so a reconciled listing
    /// (including cached legacy <c>StockDetails.Region</c> values) can be mapped to an ISO 10383 MIC.
    /// </summary>
    public static ExchangeMapping? TryLookupByVenueToken(string? venueToken)
    {
        if (string.IsNullOrWhiteSpace(venueToken))
            return null;

        var token = venueToken.Trim();

        if (_suffixMap.TryGetValue(token, out var suffixMapping))
            return suffixMapping;

        return _suffixMap.Values.FirstOrDefault(m =>
            string.Equals(m.OpenFigiExchCode, token, StringComparison.OrdinalIgnoreCase) ||
            m.CompositeOpenFigiCodes.Any(code => string.Equals(code, token, StringComparison.OrdinalIgnoreCase)) ||
            m.AlphaVantageRegions.Any(alias => string.Equals(alias, token, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Returns <c>true</c> when the OpenFIGI exchange code is a country composite (for example
    /// <c>GR</c> for Germany) rather than a specific venue.
    /// </summary>
    public static bool IsComposite(string? openFigiExchCode)
    {
        if (string.IsNullOrWhiteSpace(openFigiExchCode))
            return false;

        var token = openFigiExchCode.Trim();
        return _suffixMap.Values.Any(m =>
            m.CompositeOpenFigiCodes.Any(code => string.Equals(code, token, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Maps a broker suffix to the OpenFIGI exchange code, ISO 10383 MIC (null for composite venues
    /// with no single MIC), exchange name, the Alpha Vantage region labels that identify the same
    /// venue, its default trading currency (null when ambiguous), and the OpenFIGI country-composite
    /// codes that may stand in for the venue.
    /// </summary>
    public sealed record ExchangeMapping(
        string OpenFigiExchCode,
        string? Mic,
        string ExchangeName,
        string[] AlphaVantageRegions,
        string? DefaultCurrency,
        string[] CompositeOpenFigiCodes)
    {
        public bool MatchesRegion(string? avRegion)
        {
            if (string.IsNullOrWhiteSpace(avRegion))
                return false;

            return AlphaVantageRegions.Any(alias =>
                avRegion.Contains(alias, StringComparison.OrdinalIgnoreCase) ||
                alias.Contains(avRegion, StringComparison.OrdinalIgnoreCase));
        }
    }
}