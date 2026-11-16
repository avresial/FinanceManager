using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
namespace FinanceManager.Domain.FinancialAccounts.Bond.Dtos;

public class BondAccountDto
{
    public Dictionary<int, BondAccountEntryDto>? NextOlderEntries { get; set; }
    public Dictionary<int, BondAccountEntryDto>? NextYoungerEntries { get; set; }
    public IEnumerable<BondAccountEntryDto> Entries { get; set; } = [];
    public AccountType AccountType { get; set; }
    public AccountLabel AccountLabel { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrencyId { get; set; }

}