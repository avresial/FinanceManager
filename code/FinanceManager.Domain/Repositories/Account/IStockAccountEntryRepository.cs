namespace FinanceManager.Domain.Repositories.Account;

public interface IStockAccountEntryRepository<T> : IAccountEntryRepository<T>
{
    new Task<Dictionary<string, T>> GetNextOlder(int accountId, DateTime date);
    new Task<Dictionary<string, T>> GetNextYounger(int accountId, DateTime date);
}