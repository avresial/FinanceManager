
namespace FinanceManager.Domain.Entities.Shared.Accounts;

public class FinancialEntryBase(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange)
{
    public int EntryId { get; internal set; } = entryId;
    public int AccountId { get; set; } = accountId;
    public DateTime PostingDate { get; set; } = postingDate;

    public decimal Value { get; set; } = value;
    public decimal ValueChange { get; set; } = valueChange;

    public ICollection<FinancialLabel> Labels { get; set; } = [];

    public virtual void Update(FinancialEntryBase financialEntryBase)
    {
        if (EntryId != financialEntryBase.EntryId) throw new Exception("Entry id can not be changed.");

        PostingDate = financialEntryBase.PostingDate;
        var valueChangeChange = financialEntryBase.ValueChange - ValueChange;
        Value += valueChangeChange;

        ValueChange = financialEntryBase.ValueChange;
        AccountId = financialEntryBase.AccountId;
        Labels = financialEntryBase.Labels ?? [];
    }
}