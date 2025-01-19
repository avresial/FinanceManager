using FinanceManager.Domain.Entities.Accounts;

namespace FinanceManager.Domain.Repositories.Account
{
    public interface IBankAccountRepository
    {
        BankAccount? Get(int accountId);
        bool Add(int userId, string accountName);
        bool Update(int accountId, string accountName);
        bool Delete(int accountId);
    }
}
