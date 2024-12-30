using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.Core.Repositories
{
    public interface IFinancalAccountRepository
    {
        public Dictionary<int, Type> GetAvailableAccounts();
        public int GetLastAccountId();
        public DateTime? GetStartDate(int id);
        public DateTime? GetEndDate(int id);

        public bool AccountExists(int id);
        public T? GetAccount<T>(int id, DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase;
        public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase;

        public void AddAccount<T>(T account) where T : FinancialAccountBase;
        public void AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data) where AccountType : FinancialAccountBase where EntryType : FinancialEntryBase;
        public void UpdateAccount<T>(T account) where T : FinancialAccountBase;
        public void RemoveAccount(int id);

        public void AddEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
        public void UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
        public void RemoveEntry(int accountEntryId, int id);

    }
}
