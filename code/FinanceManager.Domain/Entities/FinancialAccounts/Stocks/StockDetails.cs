using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Entities.Stocks;

public class StockDetails
{
    public required string Ticker { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public required Currency Currency { get; set; }
}
