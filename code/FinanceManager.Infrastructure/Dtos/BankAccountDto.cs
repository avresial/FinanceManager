using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos;

public class BankAccountDto : BankAccountInformationsDto
{
    public IEnumerable<BankAccountEntryDto> Entries { get; set; } = [];
};

public class BankAccountInformationsDto : FinancialAccountBaseDto
{
    public DateTime? OlderThanLoadedEntry { get; set; }
    public DateTime? YoungerThanLoadedEntry { get; set; }
    public AccountType AccountType { get; set; }
};
