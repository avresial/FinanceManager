
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Entities;

public class FinancialEntryBase(int accountId, long entryId, DateTime postingDate, decimal value, decimal valueChange)
    : AccountEntryBase(accountId, entryId)
{
    [JsonPropertyOrder(3)]
    public DateTime PostingDate { get; set; } = postingDate;

    [JsonPropertyOrder(4)]
    public decimal Value { get; set; } = value;

    [JsonPropertyOrder(5)]
    public decimal ValueChange { get; set; } = valueChange;

    [JsonPropertyOrder(6)]
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