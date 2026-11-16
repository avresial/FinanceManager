using FinanceManager.Contracts.FinancialAccounts.Shared;
namespace FinanceManager.Contracts.FinancialAccounts.Bond;

public class BondAccountDto
{
    public Dictionary<int, BondAccountEntryDto>? NextOlderEntries { get; set; }
    public Dictionary<int, BondAccountEntryDto>? NextYoungerEntries { get; set; }
    public IEnumerable<BondAccountEntryDto> Entries { get; set; } = [];
    public AccountTypeDto AccountType { get; set; }
    public AccountLabelDto AccountLabel { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrencyId { get; set; }

}