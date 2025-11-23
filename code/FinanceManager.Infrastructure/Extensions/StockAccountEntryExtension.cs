using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Extensions;

public static class StockAccountEntryExtension
{
    public static StockAccountEntryDto ToDto(this StockAccountEntry stockAccountEntry) => new()
    {
        AccountId = stockAccountEntry.AccountId,
        EntryId = stockAccountEntry.EntryId,
        ValueChange = stockAccountEntry.ValueChange,
        Value = stockAccountEntry.Value,
        Ticker = stockAccountEntry.Ticker,
        PostingDate = stockAccountEntry.PostingDate,
    };
}