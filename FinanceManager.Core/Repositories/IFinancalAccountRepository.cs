using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.Core.Repositories
{
    public interface IFinancalAccountRepository
    {
        public Dictionary<string, Type> GetAvailableAccounts();
        public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase;
        public T? GetAccount<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase;
        public List<T>? GetEntries<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialEntryBase;

        public void AddFinancialAccount<T>(T account) where T : FinancialAccountBase;
        public void AddFinancialAccount<AccountType, EntryType>(string accountName, List<EntryType> data) where AccountType : FinancialAccountBase where EntryType : FinancialEntryBase;
        public void AddFinancialEntry<T>(T accountEntry, string accountName) where T : FinancialEntryBase;
        public void UpdateFinancialEntry<T>(T accountEntry, string accountName) where T : FinancialEntryBase;
        public void RemoveFinancialEntry(int accountEntryId, string accountName);

        public bool AccountExists(string accountName);
    }
}
