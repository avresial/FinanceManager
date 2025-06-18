using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos
{
    public class StockAccountEntryDto : FinancialEntryBaseDto
    {
        public required string Ticker { get; set; }
        public InvestmentType InvestmentType { get; set; }

        public StockAccountEntry ToStockAccountEntry() => new(AccountId, EntryId, PostingDate, Value, ValueChange, Ticker, InvestmentType);
    }
}
