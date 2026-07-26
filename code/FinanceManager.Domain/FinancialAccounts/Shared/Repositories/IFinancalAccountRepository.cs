using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Repositories;

public interface IFinancialAccountRepository
{
    public Task<Dictionary<int, Type>> GetAvailableAccounts(int userId);
    public Task<int> GetAccountsCount();
    public Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;
    public IAsyncEnumerable<T> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation;

    public Task<int?> AddAccount<T>(T account) where T : BasicAccountInformation;
    public Task UpdateAccount<T>(T account) where T : BasicAccountInformation;
    public Task RemoveAccount(Type accountType, int id);
}