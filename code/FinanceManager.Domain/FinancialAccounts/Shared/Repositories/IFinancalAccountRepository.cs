using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Repositories;

public interface IFinancialAccountRepository
{
    public Task<Dictionary<int, Type>> GetAvailableAccounts(int userId);
    public Task<int> GetAccountsCount();
    public Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : class, IAccount;
    public IAsyncEnumerable<T> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd, bool includeEntryMetadata = true) where T : class, IAccount;

    public Task<int?> AddAccount<T>(T account) where T : class, IAccount;
    public Task RemoveAccount(Type accountType, int id);
}