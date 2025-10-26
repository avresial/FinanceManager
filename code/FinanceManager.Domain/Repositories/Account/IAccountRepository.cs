using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Domain.Repositories.Account;

public interface IAccountRepository<T>
{
    Task<int> GetAccountsCount();
    Task<int?> GetLastAccountId();
    IAsyncEnumerable<AvailableAccount> GetAvailableAccounts(int userId);
    Task<T?> Get(int accountId);
    Task<bool> Exists(int accountId);
    Task<int?> Add(int userId, string accountName);
    Task<bool> Update(int accountId, string accountName);
    Task<bool> Delete(int accountId);
}
