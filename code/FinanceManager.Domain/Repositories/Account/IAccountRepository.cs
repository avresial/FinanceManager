using FinanceManager.Domain.Enums;
using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Domain.Repositories.Account
{
    public interface IBankAccountRepository<T> : IAccountRepository<T>
    {
        int? Add(int accountId, int userId, string accountName, AccountType accountType);
        bool Update(int accountId, string accountName, AccountType accountType);
    }

    public interface IAccountRepository<T>
    {
        IEnumerable<AvailableAccount> GetAvailableAccounts(int userId);
        T? Get(int accountId);
        int? Add(int accountId, int userId, string accountName);
        bool Update(int accountId, string accountName);
        bool Delete(int accountId);
    }
}
