using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Infrastructure.Dtos;

public class StockAccountEntryDto : FinancialEntryBaseDto
{
    public string Isin { get; set; } = string.Empty;
    public required string Ticker { get; set; }
    public InvestmentType InvestmentType { get; set; }

    public StockAccountEntry ToStockAccountEntry() => new(AccountId, EntryId, PostingDate, Value, ValueChange, Isin, InvestmentType)
    {
        Ticker = Ticker
    };
}