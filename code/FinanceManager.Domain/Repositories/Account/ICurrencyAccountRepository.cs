using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Repositories.Account;

public interface ICurrencyAccountRepository<T> : IAccountRepository<T>
{
    Task<int?> Add(int userId, string accountName, AccountLabel accountType);
    Task<bool> Update(int accountId, string accountName, AccountLabel accountType);
}