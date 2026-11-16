using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

public interface IFinancialAccountService
{
    public Task<Dictionary<int, Type>> GetAvailableAccounts();
    public Task<Dictionary<int, string>> GetAvailableAccountNames();
    public Task<DateTime?> GetStartDate(int accountId);
    public Task<DateTime?> GetEndDate(int accountId);

    public Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd, int minimumEntryCount = 0) where T : class, IAccount;
    public Task<T?> GetInitialTransactionHistory<T>(int userId, int id, DateTime dateStart, DateTime dateEnd,
        int minimumEntryCount = 100) where T : class, IAccount;

    public Task AddAccount<T>(T account) where T : class, IAccount;
    public Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data) where AccountType : class, IAccount where EntryType : class, ILedgerEntry;
    public Task UpdateAccount<T>(T account) where T : class, IAccount;
    public Task RemoveAccount(int id);

    public Task AddEntry<T>(T accountEntry) where T : class, ILedgerEntry;
    public Task UpdateEntry<T>(T accountEntry) where T : class, ILedgerEntry;
    public Task RemoveEntry(long accountEntryId, int accountId);
    public void InitializeMock();
}