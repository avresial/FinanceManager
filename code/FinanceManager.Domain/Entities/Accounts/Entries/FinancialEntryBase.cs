using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.Domain.Entities.Accounts;

public class FinancialEntryBase
{
    public int EntryId { get; internal set; }
    public int AccountId { get; set; }
    public DateTime PostingDate { get; set; }

    public decimal Value { get; set; }
    public decimal ValueChange { get; set; }

    public List<FinancialLabel> Labels { get; set; } = [new() { Name = "Test" }];

    public FinancialEntryBase(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange)
    {
        AccountId = accountId;
        EntryId = entryId;
        PostingDate = postingDate;
        Value = value;
        ValueChange = valueChange;
    }

    public virtual void Update(FinancialEntryBase financialEntryBase)
    {
        if (EntryId != financialEntryBase.EntryId) throw new Exception("Entry id can not be changed.");

        PostingDate = financialEntryBase.PostingDate;
        var valueChangeChange = financialEntryBase.ValueChange - ValueChange;
        Value += valueChangeChange;

        ValueChange = financialEntryBase.ValueChange;
        AccountId = financialEntryBase.AccountId;
        Labels = financialEntryBase.Labels;
    }
}
