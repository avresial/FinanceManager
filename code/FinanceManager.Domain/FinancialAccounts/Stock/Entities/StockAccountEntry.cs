using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Entities;

public class StockAccountEntry : FinancialEntryBase
{
    /// <summary>ISIN is the authoritative key for this entry — all lookups must use Isin, not Ticker.</summary>
    public string Isin { get; set; }
    /// <summary>Display-only broker ticker alias (e.g. "CSPX.UK"). Not used as a lookup or join key.</summary>
    public string Ticker { get; set; }
    public InvestmentType InvestmentType { get; set; }

    public StockAccountEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange, string isin, InvestmentType investmentType)
        : base(accountId, entryId, postingDate, value, valueChange)
    {
        Isin = isin;
        Ticker = string.Empty;
        InvestmentType = investmentType;
    }

    public void Update(StockAccountEntry entry)
    {
        PostingDate = entry.PostingDate;

        var valueChangeChange = entry.ValueChange - ValueChange;
        Value += valueChangeChange;

        ValueChange = entry.ValueChange;
        Isin = entry.Isin;
        Ticker = entry.Ticker;
        InvestmentType = entry.InvestmentType;
    }

    public StockAccountEntry GetCopy() => new StockAccountEntry(AccountId, EntryId, PostingDate, Value, ValueChange, Isin, InvestmentType);
}