using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Infrastructure.Extensions;

public static class CurrencyAccountEntryExtension
{
    public static CurrencyAccountEntryDto ToDto(this CurrencyAccountEntry currencyAccountEntry) => new()
    {
        AccountId = currencyAccountEntry.AccountId,
        EntryId = currencyAccountEntry.EntryId,
        ValueChange = currencyAccountEntry.ValueChange,
        Value = currencyAccountEntry.Value,
        Description = currencyAccountEntry.Description,
        PostingDate = currencyAccountEntry.PostingDate,
        Labels = [.. currencyAccountEntry.Labels.Select(x => new FinancialLabel() { Name = x.Name, Id = x.Id })]
    };
}