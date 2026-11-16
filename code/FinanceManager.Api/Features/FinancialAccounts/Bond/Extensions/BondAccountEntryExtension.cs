using FinanceManager.Contracts.FinancialAccounts.Bond;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Api.Features.FinancialAccounts.Bond.Extensions;

public static class BondAccountEntryExtension
{
    public static BondAccountEntryDto ToDto(this BondAccountEntry bondAccountEntry) => new()
    {
        AccountId = bondAccountEntry.AccountId,
        EntryId = bondAccountEntry.EntryId,
        ValueChange = bondAccountEntry.ValueChange,
        Value = bondAccountEntry.Value,
        BondDetailsId = bondAccountEntry.BondDetailsId,
        Description = bondAccountEntry.Description,
        PostingDate = bondAccountEntry.PostingDate,
        Labels = [.. bondAccountEntry.Labels.Select(x => new FinanceManager.Contracts.FinancialAccounts.Shared.FinancialLabelDto() { Name = x.Name, Id = x.Id })]
    };

}