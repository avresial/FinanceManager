using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Extensions;
public static class StockAccountEntryExtension
{
    public static StockAccountEntryDto ToDto(this StockAccountEntry bankAccountEntry)
    {
        return new StockAccountEntryDto()
        {
            AccountId = bankAccountEntry.AccountId,
            EntryId = bankAccountEntry.EntryId,
            ValueChange = bankAccountEntry.ValueChange,
            Value = bankAccountEntry.Value,
            Ticker = bankAccountEntry.Ticker,
            PostingDate = bankAccountEntry.PostingDate,
        };
    }
}
