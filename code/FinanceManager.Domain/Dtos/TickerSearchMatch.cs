namespace FinanceManager.Domain.Dtos;

public class TickerSearchMatch
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string MarketOpen { get; set; } = string.Empty;
    public string MarketClose { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
}