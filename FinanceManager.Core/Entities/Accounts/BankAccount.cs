using FinanceManager.Core.Enums;
using FinanceManager.Core.Extensions;

namespace FinanceManager.Core.Entities.Accounts
{
    public class BankAccount : FinancialAccountBase<BankAccountEntry>
    {
        public AccountType AccountType { get; private set; }

        public BankAccount(int id, string name, IEnumerable<BankAccountEntry> entries, AccountType accountType) : base(id, name)
        {
            Entries = entries.ToList();
            AccountType = accountType;
        }
        public BankAccount(int id, string name, AccountType accountType) : base(id, name)
        {
            AccountType = accountType;
            Entries = new List<BankAccountEntry>();
        }

        public void Add(AddBankEntryDto entry)
        {
            Entries ??= new List<BankAccountEntry>();
            var alredyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.ValueChange == entry.ValueChange);
            if (alredyExistingEntry is not null)
            {
                throw new Exception($"WARNING - Entry already exist, can not be added: Id:{alredyExistingEntry.Id}, Posting date{alredyExistingEntry.PostingDate}, " +
                    $"Value change {alredyExistingEntry.ValueChange}");
            }

            var previousEntry = Entries.GetPrevious(entry.PostingDate).FirstOrDefault();
            var index = -1;

            if (previousEntry is not null)
                index = Entries.IndexOf(previousEntry);

            BankAccountEntry newEntry = null;
            if (index == -1)
            {
                index = Entries.Count();
                newEntry = new BankAccountEntry(GetNextFreeId(), entry.PostingDate, entry.ValueChange, entry.ValueChange);
                Entries.Add(newEntry);
                index -= 1;
            }
            else
            {
                newEntry = new BankAccountEntry(GetNextFreeId(), entry.PostingDate, entry.ValueChange, entry.ValueChange);
                Entries.Insert(index, newEntry);
            }

            RecalculateEntryValues(index);
        }
        public override void Update(BankAccountEntry entry, bool recalculateValues = true)
        {
            Entries ??= new List<BankAccountEntry>();

            var entryToUpdate = Entries.FirstOrDefault(x => x.Id == entry.Id);
            if (entryToUpdate is null) return;

            entryToUpdate.Update(entry);
            Entries.Remove(entryToUpdate);
            var previousEntry = Entries.GetPrevious(entryToUpdate.PostingDate).FirstOrDefault();

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
        public virtual void GetEntry(DateTime start)
        {
            throw new NotImplementedException();
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
