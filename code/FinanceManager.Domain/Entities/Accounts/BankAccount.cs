using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.Entities.Accounts
{
    public class BankAccount : FinancialAccountBase<BankAccountEntry>
    {
        public readonly BankAccountEntry? NextOlderEntry = null;
        public readonly BankAccountEntry? NextYoungerEntry = null;
        public AccountLabel AccountType { get; set; }

        [JsonConstructorAttribute]
        public BankAccount(int userId, int accountId, string name, IEnumerable<BankAccountEntry>? entries = null, AccountLabel accountType = AccountLabel.Other,
            BankAccountEntry? nextOlderEntry = null, BankAccountEntry? nextYoungerEntry = null) : base(userId, accountId, name)
        {
            this.UserId = userId;
            Entries = entries is null ? ([]) : entries.ToList();
            AccountType = accountType;
            NextOlderEntry = nextOlderEntry;
            NextYoungerEntry = nextYoungerEntry;
        }
        public BankAccount(int userId, int id, string name, AccountLabel accountType) : base(userId, id, name)
        {
            AccountType = accountType;
            Entries = [];
        }

        public BankAccountEntry? GetThisOrNextOlder(DateTime date)
        {
            if (Entries is null) return default;
            var result = Entries.GetThisOrNextOlder(date);

            if (result is not null) return result;

            return NextOlderEntry;
        }
        public void AddEntry(AddBankEntryDto entry)
        {
            Entries ??= [];
            var alreadyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.ValueChange == entry.ValueChange);
            if (alreadyExistingEntry is not null)
            {
                Debug.WriteLine($"WARNING - Entry already exist, can not be added: Id:{alreadyExistingEntry.EntryId}, Posting date{alreadyExistingEntry.PostingDate}, Value change {alreadyExistingEntry.ValueChange}");
                //throw new Exception($"Entry already exist, can not be added - Posting date: {alreadyExistingEntry.PostingDate}, " +
                //    $"Value change: {alreadyExistingEntry.ValueChange}");
            }

            var previousEntry = Entries.GetNextYounger(entry.PostingDate).FirstOrDefault();
            var index = -1;

            if (previousEntry is not null)
                index = Entries.IndexOf(previousEntry);

            var newEntry = new BankAccountEntry(AccountId, GetNextFreeId(), entry.PostingDate, entry.ValueChange, entry.ValueChange)
            {
                Description = entry.Description,
                ExpenseType = entry.ExpenseType,
                Labels = entry.labels
            };


            if (index == -1)
            {
                index = Entries.Count;
                Entries.Add(newEntry);
                index -= 1;
            }
            else
            {
                Entries.Insert(index, newEntry);
            }

            RecalculateEntryValues(index);
        }
        public override void UpdateEntry(BankAccountEntry entry, bool recalculateValues = true)
        {
            Entries ??= [];

            var entryToUpdate = Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);
            if (entryToUpdate is null) return;

            entryToUpdate.Update(entry);
            Entries.Remove(entryToUpdate);
            var previousEntry = Entries.GetNextYounger(entryToUpdate.PostingDate).FirstOrDefault();

            if (previousEntry is null)
            {
                Entries.Add(entryToUpdate);
            }
            else
            {
                var newIndex = Entries.IndexOf(previousEntry);
                Entries.Insert(newIndex, entryToUpdate);
            }

            var index = Entries.IndexOf(entryToUpdate);
            if (recalculateValues)
                RecalculateEntryValues(index);
        }

        public int GetNextFreeId()
        {
            var currentMaxId = GetMaxId();
            if (currentMaxId is not null)
                return currentMaxId.Value + 1;
            return 0;
        }

    }
}
