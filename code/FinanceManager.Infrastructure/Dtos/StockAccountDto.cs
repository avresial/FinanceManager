namespace FinanceManager.Infrastructure.Dtos;

public class StockAccountDto : FinancialAccountBaseDto
{
    public Dictionary<string, StockAccountEntryDto>? NextOlderEntries { get; set; }
    public Dictionary<string, StockAccountEntryDto>? NextYoungerEntries { get; set; }
    public IEnumerable<StockAccountEntryDto> Entries { get; set; } = [];
}
