using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.Entities.Bonds;

public class BondAccountEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange, int bondDetailsId) : FinancialEntryBase(accountId, entryId, postingDate, value, valueChange)
{
    public int BondDetailsId { get; set; } = bondDetailsId;

    public void Update(BondAccountEntry entry)
    {
        base.Update(entry);

        BondDetailsId = entry.BondDetailsId;
    }

    public BondAccountEntry GetCopy() => new(AccountId, EntryId, PostingDate, Value, ValueChange, BondDetailsId)
    {
        Labels = this.Labels,
    };

    public override string ToString() => $"PostingDate: {PostingDate}, EntryId: {EntryId}, Value: {Value}, ValueChange: {ValueChange}, BondDetailsId: {BondDetailsId}";
}