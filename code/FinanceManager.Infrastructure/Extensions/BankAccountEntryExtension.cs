using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Extensions;
public static class BankAccountEntryExtension
{
    public static BankAccountEntryDto ToDto(this BankAccountEntry bankAccountEntry) =>
         new()
         {
             AccountId = bankAccountEntry.AccountId,
             EntryId = bankAccountEntry.EntryId,
             ValueChange = bankAccountEntry.ValueChange,
             Value = bankAccountEntry.Value,
             Description = bankAccountEntry.Description,
             PostingDate = bankAccountEntry.PostingDate,
         };
}
