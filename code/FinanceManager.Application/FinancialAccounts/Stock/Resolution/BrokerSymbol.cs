namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Normalizes a broker ticker (e.g., "CSPX.UK") into base ticker + exchange hint.
/// Maintains a lookup table of broker suffixes to OpenFIGI exchange codes and Alpha Vantage regions.
/// </summary>
public sealed class BrokerSymbol
{
    public required string BaseTicker { get; set; }
    public string? ExchangeHint { get; set; }

    // Broker suffix → (OpenFIGI exchange code, Alpha Vantage region aliases).
    // Alpha Vantage's SYMBOL_SEARCH "region" is a free-form label ("United Kingdom", not "GB"),
    // so each suffix carries the aliases we accept when correlating an AV match to the hint.
    private static readonly Dictionary<string, ExchangeMapping> _suffixMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "UK", new("LN", ["United Kingdom", "GB", "UK", "London"]) },         // London Stock Exchange
        { "DE", new("GY", ["Germany", "XETRA", "Frankfurt", "DE"]) },          // Xetra (Deutsche Börse)
        { "PL", new("WA", ["Poland", "Warsaw", "PL"]) },                       // Warsaw Stock Exchange
        { "US", new("US", ["United States", "US", "USA"]) },                   // US (NASDAQ/NYSE)
        { "CA", new("CT", ["Canada", "Toronto", "CA"]) },                      // Canada
        { "AU", new("AU", ["Australia", "AU"]) },                              // Australia
        { "JP", new("TT", ["Japan", "Tokyo", "JP"]) },                         // Tokyo
        { "HK", new("HK", ["Hong Kong", "HK"]) },                              // Hong Kong
        { "SG", new("SI", ["Singapore", "SG"]) },                              // Singapore
        { "CH", new("VX", ["Switzerland", "Swiss", "CH"]) },                   // Swiss SIX
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
    /// Maps a broker suffix to the OpenFIGI exchange code and the Alpha Vantage region labels
    /// that identify the same venue.
    /// </summary>
    public sealed record ExchangeMapping(string OpenFigiExchCode, string[] AlphaVantageRegions)
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