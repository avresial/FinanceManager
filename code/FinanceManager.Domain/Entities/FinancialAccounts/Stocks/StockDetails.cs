using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Entities.Stocks;

/// <summary>
/// Represents a stock or equity security.
/// Identified by ISIN (International Securities Identification Number) which is the canonical identifier.
/// Ticker is preserved for external API lookups (e.g., Alpha Vantage).
/// </summary>
public class StockDetails
{
    public required string Isin { get; set; }
    public required string Ticker { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public required Currency Currency { get; set; }
}