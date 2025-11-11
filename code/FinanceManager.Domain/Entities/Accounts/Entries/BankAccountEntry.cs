namespace FinanceManager.Domain.Entities.Accounts.Entries;

public class BankAccountEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange)
    : FinancialEntryBase(accountId, entryId, postingDate, value, valueChange)
{
    public string Description { get; set; } = string.Empty;

    public void Update(BankAccountEntry entry)
    {
        base.Update(entry);

        Description = entry.Description;
    }

    public BankAccountEntry GetCopy() => new(AccountId, EntryId, PostingDate, Value, ValueChange)
    {
        Description = this.Description,
    };

    public override string ToString() => $"PostingDate: {PostingDate}, EntryId: {EntryId}, Value: {Value}, ValueChange: {ValueChange}";
}