using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Accounts.Entries
{
    public class BankAccountEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange)
        : FinancialEntryBase(accountId, entryId, postingDate, value, valueChange)
    {
        public string Description { get; set; } = string.Empty;
        public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;

        public void Update(BankAccountEntry entry)
        {
            PostingDate = entry.PostingDate;
            var valueChangeChange = entry.ValueChange - ValueChange;
            Value += valueChangeChange;

            ValueChange = entry.ValueChange;
            Description = entry.Description;
            ExpenseType = entry.ExpenseType;
        }

        public BankAccountEntry GetCopy()
        {
            return new BankAccountEntry(AccountId, EntryId, PostingDate, Value, ValueChange);
        }
    }
}
