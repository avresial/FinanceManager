using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Extensions;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Extensions;

public static class CurrencyAccountEntryExtension
{
    public static CurrencyAccountEntryDto ToDto(this CurrencyAccountEntry currencyAccountEntry) => new()
    {
        AccountId = currencyAccountEntry.AccountId,
        EntryId = currencyAccountEntry.EntryId,
        ValueChange = currencyAccountEntry.ValueChange,
        Value = currencyAccountEntry.Value,
        Description = currencyAccountEntry.Description,
        ContractorDetails = currencyAccountEntry.ContractorDetails,
        PostingDate = currencyAccountEntry.PostingDate,
        Labels = [.. currencyAccountEntry.Labels.Select(x => new FinancialLabel() { Name = x.Name, Id = x.Id })]
    };
}