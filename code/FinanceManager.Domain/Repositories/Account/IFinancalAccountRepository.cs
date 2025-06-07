using FinanceManager.Domain.Entities.Accounts;

namespace FinanceManager.Domain.Repositories.Account
{
    public interface IFinancalAccountRepository
    {
        public Task<Dictionary<int, Type>> GetAvailableAccounts(int userId);
        public Task<int> GetLastAccountId();
        public Task<int> GetAccountsCount();
        public Task<DateTime?> GetStartDate(int id);
        public Task<DateTime?> GetEndDate(int id);

        public Task<bool> AccountExists(int id);
        public Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;
        public Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;

        public Task<int?> AddAccount<T>(T account) where T : BasicAccountInformation;
        public Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data) where AccountType : BasicAccountInformation where EntryType : FinancialEntryBase;
        public Task UpdateAccount<T>(T account) where T : BasicAccountInformation;
        public Task RemoveAccount(int id);

        public Task AddEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
        public Task UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
        public Task RemoveEntry(int accountEntryId, int id);
    }
}
