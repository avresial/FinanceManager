using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.Entities.FinancialAccounts.Currency;

public class CurrencyAccountEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange)
    : FinancialEntryBase(accountId, entryId, postingDate, value, valueChange)
{
    public string Description { get; set; } = string.Empty;

    public void Update(CurrencyAccountEntry entry)
    {
        base.Update(entry);

        Description = entry.Description;
    }

    public CurrencyAccountEntry GetCopy() => new(AccountId, EntryId, PostingDate, Value, ValueChange)
    {
        Description = this.Description,
    };

    public override string ToString() => $"PostingDate: {PostingDate}, EntryId: {EntryId}, Value: {Value}, ValueChange: {ValueChange}";
}