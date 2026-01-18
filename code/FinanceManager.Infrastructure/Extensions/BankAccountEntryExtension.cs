using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.FinancialAccounts.Currency;
using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Infrastructure.Extensions;

public static class BankAccountEntryExtension
{
    public static CurrencyAccountEntryDto ToDto(this CurrencyAccountEntry bankAccountEntry) => new()
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