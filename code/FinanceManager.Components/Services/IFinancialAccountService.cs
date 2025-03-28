using FinanceManager.Domain.Entities.Accounts;

namespace FinanceManager.Components.Services;

public interface IFinancialAccountService
{
    public Task<Dictionary<int, Type>> GetAvailableAccounts();
    public Task<int?> GetLastAccountId();
    public Task<DateTime?> GetStartDate(int accountId);
    public Task<DateTime?> GetEndDate(int accountId);

    public Task<bool> AccountExists(int accountId);
    public Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;
    public Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;

    public Task AddAccount<T>(T account) where T : BasicAccountInformation;
    public Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data) where AccountType : BasicAccountInformation where EntryType : FinancialEntryBase;
    public Task UpdateAccount<T>(T account) where T : BasicAccountInformation;
    public Task RemoveAccount(int id);

    public Task AddEntry<T>(T accountEntry) where T : FinancialEntryBase;
    public Task UpdateEntry<T>(T accountEntry) where T : FinancialEntryBase;
    public Task RemoveEntry(int accountEntryId, int accountId);
    public void InitializeMock();
}
