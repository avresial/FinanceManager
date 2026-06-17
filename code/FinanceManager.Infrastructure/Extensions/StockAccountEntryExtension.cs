using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
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
        Isin = stockAccountEntry.Isin,
        Ticker = stockAccountEntry.Ticker,
        InvestmentType = stockAccountEntry.InvestmentType,
        PostingDate = stockAccountEntry.PostingDate,
    };
}