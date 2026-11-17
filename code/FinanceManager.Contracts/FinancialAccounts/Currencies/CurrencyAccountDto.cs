using FinanceManager.Contracts.FinancialAccounts.Shared;

namespace FinanceManager.Contracts.FinancialAccounts.Currencies;

public class CurrencyAccountDto
{
    public CurrencyAccountEntryDto? NextOlderEntry { get; set; }
    public CurrencyAccountEntryDto? NextYoungerEntry { get; set; }
    public IEnumerable<CurrencyAccountEntryDto> Entries { get; set; } = [];
    public AccountTypeDto AccountType { get; set; }
    public AccountLabelDto AccountLabel { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrencyId { get; set; }

};