namespace FinanceManager.Domain.Repositories.Account
{
    public interface IAccountEntryRepository<T>
    {
        IEnumerable<T> Get(int accountId, DateTime startDate, DateTime endDate);
        T? GetYoungest(int accountId);
        T? GetNextYounger(int accountId, int entryId);
        T? GetNextOlder(int accountId, int entryId);
        T? GetOldest(int accountId);

        bool Add(T entry);
        bool Update(T entry);
        bool Delete(int accountId, int entryId);
    }
}
