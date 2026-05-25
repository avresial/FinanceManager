using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Domain.Dtos;

public class CurrencyAccountDto : FinancialAccountBaseDto
{
    public CurrencyAccountEntryDto? NextOlderEntry { get; set; }
    public CurrencyAccountEntryDto? NextYoungerEntry { get; set; }
    public IEnumerable<CurrencyAccountEntryDto> Entries { get; set; } = [];
};