using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Extensions;

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

    public static BondAccountEntry ToBondAccountEntry(this BondAccountEntryDto bondAccountEntryDto) => new(
        bondAccountEntryDto.AccountId,
        bondAccountEntryDto.EntryId,
        bondAccountEntryDto.PostingDate,
        bondAccountEntryDto.Value,
        bondAccountEntryDto.ValueChange,
        bondAccountEntryDto.BondDetailsId)
    {
        Labels = bondAccountEntryDto.Labels
    };
}