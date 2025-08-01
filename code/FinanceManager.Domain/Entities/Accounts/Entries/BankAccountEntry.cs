using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Accounts.Entries;

public class BankAccountEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange)
    : FinancialEntryBase(accountId, entryId, postingDate, value, valueChange)
{
    public string Description { get; set; } = string.Empty;
    public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;

    public void Update(BankAccountEntry entry)
    {
        base.Update(entry);

        Description = entry.Description;
        ExpenseType = entry.ExpenseType;
    }

    public BankAccountEntry GetCopy() => new(AccountId, EntryId, PostingDate, Value, ValueChange)
    {
        Description = this.Description,
        ExpenseType = this.ExpenseType
    };
}
