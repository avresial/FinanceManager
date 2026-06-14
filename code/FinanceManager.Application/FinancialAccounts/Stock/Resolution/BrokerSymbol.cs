namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Normalizes a broker ticker (e.g., "CSPX.UK") into base ticker + exchange hint.
/// Maintains a lookup table of broker suffixes to OpenFIGI exchange codes and Alpha Vantage regions.
/// </summary>
public sealed class BrokerSymbol
{
    public required string BaseTicker { get; set; }
    public string? ExchangeHint { get; set; }

    private static readonly Dictionary<string, (string OpenFigiExchCode, string AlphaVantageRegion)> _suffixMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "UK", ("LN", "GB") },         // London Stock Exchange
        { "DE", ("GY", "DE") },         // Xetra (Deutsche Börse)
        { "PL", ("WA", "PL") },         // Warsaw Stock Exchange
        { "US", ("US", "US") },         // US (NASDAQ/NYSE)
        { "CA", ("CT", "CA") },         // Canada
        { "AU", ("AU", "AU") },         // Australia
        { "JP", ("TT", "JP") },         // Tokyo
        { "HK", ("HK", "HK") },         // Hong Kong
        { "SG", ("SI", "SG") },         // Singapore
        { "CH", ("VX", "CH") },         // Swiss SIX
    };

    public static BrokerSymbol Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new BrokerSymbol { BaseTicker = string.Empty };

        var normalized = input.Trim().ToUpperInvariant();
        var parts = normalized.Split('.');

        if (parts.Length == 1)
            return new BrokerSymbol { BaseTicker = parts[0] };

        return new BrokerSymbol
        {
            BaseTicker = parts[0],
            ExchangeHint = parts[1]
        };
    }

    public (string OpenFigiExchCode, string AlphaVantageRegion)? TryLookupExchange()
    {
        if (string.IsNullOrWhiteSpace(ExchangeHint))
            return null;

        return _suffixMap.TryGetValue(ExchangeHint, out var mapping) ? mapping : null;
    }
}