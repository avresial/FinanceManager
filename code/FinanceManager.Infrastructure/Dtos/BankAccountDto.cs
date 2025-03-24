using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos;

public class BankAccountDto : FinancialAccountBaseDto
{
    public DateTime? OlderThenLoadedEntry { get; set; }
    public DateTime? YoungerThenLoadedEntry { get; set; }
    public AccountType AccountType { get; set; }
    public IEnumerable<BankAccountEntryDto> Entries { get; set; } = [];
};
