namespace FinanceManager.Domain.Repositories.Account;

public interface IStockAccountEntryRepository<T> : IAccountEntryRepository<T>
{
    Task<List<T>> Get(int accountId, DateTime date, int count, bool olderThenDate = true);
    new Task<Dictionary<string, T>> GetNextOlder(int accountId, DateTime date);
    new Task<Dictionary<string, T>> GetNextYounger(int accountId, DateTime date);
}