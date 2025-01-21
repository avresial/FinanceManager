namespace FinanceManager.Domain.Repositories.Account
{
    public interface IAccountEntryRepository<T>
    {
        T? Get(int accountId, DateTime startDate, DateTime endDate);
        T? GetYoungest(int accountId);
        T? GetOldest(int accountId);

        bool Add(T entry);
        bool Update(T entry);
        bool Delete(int accountId, int entryId);
    }
}
