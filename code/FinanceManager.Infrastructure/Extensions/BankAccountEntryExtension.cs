using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Extensions;
public static class BankAccountEntryExtension
{
    public static BankAccountEntryDto ToDto(this BankAccountEntry bankAccountEntry)
    {
        return new BankAccountEntryDto()
        {
            AccountId = bankAccountEntry.AccountId,
            EntryId = bankAccountEntry.EntryId,
            ValueChange = bankAccountEntry.ValueChange,
            Value = bankAccountEntry.Value,
            Description = bankAccountEntry.Description,
            ExpenseType = bankAccountEntry.ExpenseType,
            PostingDate = bankAccountEntry.PostingDate,
        };
    }
}
