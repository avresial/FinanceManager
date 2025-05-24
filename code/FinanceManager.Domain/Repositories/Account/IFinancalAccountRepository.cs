using FinanceManager.Domain.Entities.Accounts;

namespace FinanceManager.Domain.Repositories.Account
{
    public interface IFinancalAccountRepository
    {
        public Dictionary<int, Type> GetAvailableAccounts(int userId);
        public int GetLastAccountId();
        public int GetAccountsCount();
        public DateTime? GetStartDate(int id);
        public DateTime? GetEndDate(int id);

        public bool AccountExists(int id);
        public Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;
        public Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;

        public void AddAccount<T>(T account) where T : BasicAccountInformation;
        public void AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data) where AccountType : BasicAccountInformation where EntryType : FinancialEntryBase;
        public void UpdateAccount<T>(T account) where T : BasicAccountInformation;
        public void RemoveAccount(int id);

        public void AddEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
        public void UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
        public void RemoveEntry(int accountEntryId, int id);
    }
}
