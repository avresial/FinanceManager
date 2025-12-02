namespace FinanceManager.Infrastructure.Dtos;

public class BondAccountDto : FinancialAccountBaseDto
{
    public BondAccountEntryDto? NextOlderEntry { get; set; }
    public BondAccountEntryDto? NextYoungerEntry { get; set; }
    public IEnumerable<BondAccountEntryDto> Entries { get; set; } = [];
}