using FinanceManager.Domain.Entities.Accounts;

namespace FinanceManager.Components.Services;

public interface IFinancalAccountService
{
    public Dictionary<int, Type> GetAvailableAccounts();
    public Task<int?> GetLastAccountId();
    public DateTime? GetStartDate(int id);
    public DateTime? GetEndDate(int id);

    public Task<bool> AccountExists(int id);
    public T? GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;
    public IEnumerable<T> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;

    public Task AddAccount<T>(T account) where T : BasicAccountInformation;
    public void AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data) where AccountType : BasicAccountInformation where EntryType : FinancialEntryBase;
    public void UpdateAccount<T>(T account) where T : BasicAccountInformation;
    public void RemoveAccount(int id);

    public void AddEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
    public void UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase;
    public void RemoveEntry(int accountEntryId, int id);
    public void InitializeMock();
}
