using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;

public interface ICurrencyAccountRepository<T> : IAccountRepository<T>
{
    Task<int?> Add(int userId, string accountName, AccountLabel accountType);
    Task<bool> Update(int accountId, string accountName, AccountLabel accountType);
}