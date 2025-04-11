using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.Entities.Accounts
{
    public class BankAccount : FinancialAccountBase<BankAccountEntry>
    {
        public readonly DateTime? OlderThenLoadedEntry = default;
        public readonly DateTime? YoungerThenLoadedEntry = default;
        public AccountType AccountType { get; set; }

        [JsonConstructorAttribute]
        public BankAccount(int userId, int accountId, string name, IEnumerable<BankAccountEntry>? entries = null, AccountType accountType = AccountType.Other, DateTime? olderThenLoadedEntry = null,
            DateTime? youngerThenLoadedEntry = null) : base(userId, accountId, name)
        {
            this.UserId = userId;
            Entries = entries is null ? ([]) : entries.ToList();
            AccountType = accountType;
            OlderThenLoadedEntry = olderThenLoadedEntry;
            YoungerThenLoadedEntry = youngerThenLoadedEntry;
        }
        public BankAccount(int userId, int id, string name, AccountType accountType) : base(userId, id, name)
        {
            AccountType = accountType;
            Entries = [];
        }

        public virtual void GetEntry(DateTime start)
        {
            throw new NotImplementedException();
        }
        public void AddEntry(AddBankEntryDto entry)
        {
            Entries ??= [];
            var alredyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.ValueChange == entry.ValueChange);
            if (alredyExistingEntry is not null)
            {
                throw new Exception($"WARNING - Entry already exist, can not be added: Id:{alredyExistingEntry.EntryId}, Posting date{alredyExistingEntry.PostingDate}, " +
                    $"Value change {alredyExistingEntry.ValueChange}");
            }

            var previousEntry = Entries.GetNextYounger(entry.PostingDate).FirstOrDefault();
            var index = -1;

            if (previousEntry is not null)
                index = Entries.IndexOf(previousEntry);


            var newEntry = new BankAccountEntry(AccountId, GetNextFreeId(), entry.PostingDate, entry.ValueChange, entry.ValueChange)
            {
                Description = entry.Description,
                ExpenseType = entry.ExpenseType
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
