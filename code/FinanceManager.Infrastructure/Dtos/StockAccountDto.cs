namespace FinanceManager.Infrastructure.Dtos;

public class StockAccountDto : FinancialAccountBaseDto
{
    public Dictionary<string, DateTime>? OlderThenLoadedEntry { get; set; }
    public Dictionary<string, DateTime>? YoungerThenLoadedEntry { get; set; }
    public IEnumerable<StockAccountEntryDto> Entries { get; set; } = [];
}
