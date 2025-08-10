namespace FinanceManager.Domain.Repositories.Account;

public interface IStockAccountEntryRepository<T> : IAccountEntryRepository<T>
{
    new Task<Dictionary<string, T>> GetNextOlder(int accountId, DateTime date);
    new Task<Dictionary<string, T>> GetNextYounger(int accountId, DateTime date);
}
public interface IAccountEntryRepository<T>
{
    Task<IEnumerable<T>> Get(int accountId, DateTime startDate, DateTime endDate);
    Task<T?> Get(int accountId, int entryId);
    Task<T?> GetYoungest(int accountId);
    Task<T?> GetNextYounger(int accountId, int entryId);
    Task<T?> GetNextYounger(int accountId, DateTime date);
    Task<T?> GetNextOlder(int accountId, int entryId);
    Task<T?> GetNextOlder(int accountId, DateTime date);
    Task<T?> GetOldest(int accountId);
    Task<int?> GetCount(int accountId);

    Task<bool> Add(T entry);
    Task<bool> AddLabel(int entryId, int labelId);
    Task<bool> Update(T entry);
    Task<bool> Delete(int accountId, int entryId);
    Task<bool> Delete(int accountId);
}
