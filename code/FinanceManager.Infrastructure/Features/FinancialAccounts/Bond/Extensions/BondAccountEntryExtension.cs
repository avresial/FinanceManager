using FinanceManager.Domain.FinancialAccounts.Bond.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Extensions;

public static class BondAccountEntryExtension
{
    public static BondAccountEntryDto ToDto(this BondAccountEntry bondAccountEntry) => new()
    {
        AccountId = bondAccountEntry.AccountId,
        EntryId = bondAccountEntry.EntryId,
        ValueChange = bondAccountEntry.ValueChange,
        Value = bondAccountEntry.Value,
        BondDetailsId = bondAccountEntry.BondDetailsId,
        PostingDate = bondAccountEntry.PostingDate,
        Labels = [.. bondAccountEntry.Labels.Select(x => new FinancialLabel() { Name = x.Name, Id = x.Id })]
    };

}