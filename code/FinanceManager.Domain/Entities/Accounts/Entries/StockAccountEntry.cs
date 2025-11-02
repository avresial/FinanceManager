using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Accounts.Entries;

public class StockAccountEntry : FinancialEntryBase
{
    public string Ticker { get; set; }
    public InvestmentType InvestmentType { get; set; }
    public StockAccountEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange, string ticker, InvestmentType investmentType)
        : base(accountId, entryId, postingDate, value, valueChange)
    {
        Ticker = ticker;
        InvestmentType = investmentType;
    }
    public void Update(StockAccountEntry entry)
    {
        PostingDate = entry.PostingDate;

        var valueChangeChange = entry.ValueChange - ValueChange;
        Value += valueChangeChange;

        ValueChange = entry.ValueChange;
        Ticker = entry.Ticker;
        InvestmentType = entry.InvestmentType;
    }

    public StockAccountEntry GetCopy() => new StockAccountEntry(AccountId, EntryId, PostingDate, Value, ValueChange, Ticker, InvestmentType);
}