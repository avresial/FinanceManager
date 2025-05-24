using FinanceManager.Domain.Enums;
using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Domain.Repositories.Account
{
    public interface IBankAccountRepository<T> : IAccountRepository<T>
    {
        Task<int?> Add(int userId, int accountId, string accountName, AccountType accountType);
        Task<bool> Update(int accountId, string accountName, AccountType accountType);
    }

    public interface IAccountRepository<T>
    {
        int GetAccountsCount();
        int? GetLastAccountId();
        IEnumerable<AvailableAccount> GetAvailableAccounts(int userId);
        Task<T?> Get(int accountId);
        Task<int?> Add(int userId, int accountId, string accountName);
        Task<bool> Update(int accountId, string accountName);
        Task<bool> Delete(int accountId);
    }
}
