namespace FinanceManager.Domain.Repositories.Account;

public interface IBondAccountEntryRepository<T> : IAccountEntryRepository<T>
{
    new Task<Dictionary<int, T>> GetNextOlder(int accountId, DateTime date);
    new Task<Dictionary<int, T>> GetNextYounger(int accountId, DateTime date);
}