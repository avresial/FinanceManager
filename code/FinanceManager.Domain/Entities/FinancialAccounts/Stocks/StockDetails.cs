using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Entities.Stocks;

/// <summary>
/// Represents a stock or equity security.
/// Identified by ISIN (International Securities Identification Number) which is the canonical identifier.
/// Ticker is the broker-facing display alias (e.g., "CSPX.UK"); it is not used as a lookup key.
/// AlphaVantageSymbol is the exact symbol recognised by the Alpha Vantage price provider (e.g., "CSPX.LON").
/// </summary>
public class StockDetails
{
    public required string Isin { get; set; }
    public required string Ticker { get; set; }
    /// <summary>The exact symbol string that Alpha Vantage recognises for price lookups (e.g. "CSPX.LON"). Null until resolved.</summary>
    public string? AlphaVantageSymbol { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public required Currency Currency { get; set; }
}