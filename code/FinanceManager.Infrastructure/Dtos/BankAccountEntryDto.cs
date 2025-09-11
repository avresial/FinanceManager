using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.Infrastructure.Dtos;

public class BankAccountEntryDto : FinancialEntryBaseDto
{
    public string Description { get; set; } = string.Empty;

    public BankAccountEntry ToBankAccountEntry() => new BankAccountEntry(AccountId, EntryId, PostingDate, Value, ValueChange)
    {
        Description = Description,
    };
}
