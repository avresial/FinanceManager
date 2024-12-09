using FinanceManager.Core.Enums;
using FinanceManager.Core.Extensions;

namespace FinanceManager.Core.Entities.Accounts
{
    public class FinancialAccountBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public virtual DateTime? Start { get; protected set; }
        public virtual DateTime? End { get; protected set; }
    }

    public class FixedAssetAccount : FinancialAccountBase<FixedAssetEntry>
    {
        public FixedAssetAccount(int id, string name) : base(id, name)
        {

        }
    }

    public class BankAccount : FinancialAccountBase<BankAccountEntry>//, IFinancalAccount
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
    }
}
