using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Domain.Repositories.Account
{
    public interface IAccountRepository<T>
    {
        IEnumerable<AvailableAccount> GetAvailableAccounts(int userId);
        T? Get(int accountId);
        int? Add(int userId, string accountName);
        bool Update(int accountId, string accountName);
        bool Delete(int accountId);
    }
}
