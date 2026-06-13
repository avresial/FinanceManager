using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Domain.Repositories.Account;

public interface IAccountRepository<T>
{
    Task<int> GetAccountsCount();
    Task<int?> GetLastAccountId();
    IAsyncEnumerable<AvailableAccount> GetAvailableAccounts(int userId);

    /// <summary>
    /// Returns all of the user's accounts of this type (metadata only, no entries) in a single query.
    /// Used to batch-load accounts when composing a whole user's portfolio (issue #411).
    /// </summary>
    Task<List<T>> GetAll(int userId);
    Task<T?> Get(int accountId);
    Task<bool> Exists(int accountId);
    Task<int?> Add(int userId, string accountName);
    Task<bool> Update(int accountId, string accountName);
    Task<bool> Delete(int accountId);
}