namespace FinanceManager.Infrastructure.Dtos;

public class StockAccountDto : FinancialAccountBaseDto
{
    public Dictionary<string, DateTime>? OlderThanLoadedEntry { get; set; }
    public Dictionary<string, DateTime>? YoungerThanLoadedEntry { get; set; }
    public IEnumerable<StockAccountEntryDto> Entries { get; set; } = [];
}
