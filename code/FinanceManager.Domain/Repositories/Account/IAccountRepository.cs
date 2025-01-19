namespace FinanceManager.Domain.Repositories.Account
{
    public interface IAccountRepository<T>
    {
        IList<(int, string)> GetAvailableAccounts(int userId);
        T? Get(int accountId);
        bool Add(int userId, string accountName);
        bool Update(int accountId, string accountName);
        bool Delete(int accountId);
    }
}
