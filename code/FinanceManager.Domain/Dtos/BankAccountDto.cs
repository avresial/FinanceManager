namespace FinanceManager.Infrastructure.Dtos;

public class BankAccountDto : FinancialAccountBaseDto
{
    public BankAccountEntryDto? NextOlderEntry { get; set; }
    public BankAccountEntryDto? NextYoungerEntry { get; set; }
    public IEnumerable<BankAccountEntryDto> Entries { get; set; } = [];
};