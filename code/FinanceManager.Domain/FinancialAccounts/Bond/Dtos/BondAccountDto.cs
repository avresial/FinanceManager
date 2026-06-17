using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
namespace FinanceManager.Domain.FinancialAccounts.Bond.Dtos;

public class BondAccountDto : FinancialAccountBaseDto
{
    public Dictionary<int, BondAccountEntryDto>? NextOlderEntries { get; set; }
    public Dictionary<int, BondAccountEntryDto>? NextYoungerEntries { get; set; }
    public IEnumerable<BondAccountEntryDto> Entries { get; set; } = [];
}