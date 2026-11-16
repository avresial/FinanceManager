using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;

public class CurrencyAccountDto
{
    public CurrencyAccountEntryDto? NextOlderEntry { get; set; }
    public CurrencyAccountEntryDto? NextYoungerEntry { get; set; }
    public IEnumerable<CurrencyAccountEntryDto> Entries { get; set; } = [];
    public AccountType AccountType { get; set; }
    public AccountLabel AccountLabel { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrencyId { get; set; }

};