namespace FinanceManager.Infrastructure.Dtos;

public class BondAccountDto : FinancialAccountBaseDto
{
    public Dictionary<int, BondAccountEntryDto>? NextOlderEntries { get; set; }
    public Dictionary<int, BondAccountEntryDto>? NextYoungerEntries { get; set; }
    public IEnumerable<BondAccountEntryDto> Entries { get; set; } = [];
}