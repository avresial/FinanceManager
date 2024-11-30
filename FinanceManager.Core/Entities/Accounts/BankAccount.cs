using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities.Accounts
{
    public class FinancialAccountBase
    {
        public string Name { get; set; }
        public virtual DateTime? Start { get; protected set; }
        public virtual DateTime? End { get; protected set; }
    }

    public class FixedAssetAccount : FinancialAccountBase<FixedAssetEntry>
    {
        public FixedAssetAccount(string name) : base(name)
        {

        }

        //	public List<FixedAssetEntry>? Entries { get; private set; }
    }

    public class BankAccount : FinancialAccountBase<BankAccountEntry>//, IFinancalAccount
    {
        //public List<BankAccountEntry>? Entries { get; private set; }
        public AccountType AccountType { get; private set; }

        public BankAccount(string name, IEnumerable<BankAccountEntry> entries, AccountType accountType) : base(name)
        {
            Entries = entries.ToList();
            AccountType = accountType;
        }
        public BankAccount(string name, AccountType accountType) : base(name)
        {
            AccountType = accountType;
            Entries = new List<BankAccountEntry>();
        }
        public virtual void GetEntry(DateTime start)
        {
            throw new NotImplementedException();
        }
    }
}
