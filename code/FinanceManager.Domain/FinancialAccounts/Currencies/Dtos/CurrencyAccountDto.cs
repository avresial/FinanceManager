using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;

public class CurrencyAccountDto : FinancialAccountBaseDto
{
    public CurrencyAccountEntryDto? NextOlderEntry { get; set; }
    public CurrencyAccountEntryDto? NextYoungerEntry { get; set; }
    public IEnumerable<CurrencyAccountEntryDto> Entries { get; set; } = [];
};