using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.Core.Repositories
{
    public interface IFinancalAccountRepository
    {
        public Dictionary<string, Type> GetAvailableAccounts();
        public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase;
        public T? GetAccount<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase;
        public List<T>? GetEntries<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialEntryBase;

        public void AddFinancialAccount<T>(T bankAccount) where T : FinancialAccountBase;
        public void AddFinancialAccount<AccountType, EntryType>(string name, List<EntryType> data) where AccountType : FinancialAccountBase where EntryType : FinancialEntryBase;
        public void AddFinancialEntry<T>(T bankAccountEntry, string accountName) where T : FinancialEntryBase;

        public bool AccountExists(string name);
    }
}
