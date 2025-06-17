using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos;

public class BankAccountEntryDto : FinancialEntryBaseDto
{
    public string Description { get; set; } = string.Empty;
    public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;


    public BankAccountEntry ToBankAccountEntry() => new BankAccountEntry(AccountId, EntryId, PostingDate, Value, ValueChange)
    {
        Description = Description,
        ExpenseType = ExpenseType,
    };
}
