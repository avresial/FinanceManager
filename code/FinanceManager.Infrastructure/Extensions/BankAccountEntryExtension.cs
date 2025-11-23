using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Extensions;

public static class BankAccountEntryExtension
{
    public static BankAccountEntryDto ToDto(this BankAccountEntry bankAccountEntry) => new()
    {
        AccountId = bankAccountEntry.AccountId,
        EntryId = bankAccountEntry.EntryId,
        ValueChange = bankAccountEntry.ValueChange,
        Value = bankAccountEntry.Value,
        Description = bankAccountEntry.Description,
        PostingDate = bankAccountEntry.PostingDate,
        Labels = [.. bankAccountEntry.Labels.Select(x => new FinancialLabel() { Name = x.Name, Id = x.Id })]
    };
}