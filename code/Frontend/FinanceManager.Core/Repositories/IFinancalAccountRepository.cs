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
        public T? GetAccount<T>(int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;
        public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;

        public void AddAccount<T>(T account) where T : BasicAccountInformation;
        public void AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data) where AccountType : BasicAccountInformation where EntryType : FinancialEntryBase;
        public void UpdateAccount<T>(T account) where T : BasicAccountInformation;
        public void RemoveAccount(int id);

        public void AddEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
        public void UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
        public void RemoveEntry(int accountEntryId, int id);
        public void InitializeMock();
    }
}
