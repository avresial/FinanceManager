using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryStockEntryRepository : IAccountEntryRepository<StockAccountEntry>
{
    public bool Add(StockAccountEntry entry)
    {
        throw new NotImplementedException();
    }

    public bool Delete(int accountId, int entryId)
    {
        throw new NotImplementedException();
    }

    public StockAccountEntry? Get(int accountId, DateTime startDate, DateTime endDate)
    {
        throw new NotImplementedException();
    }

    public StockAccountEntry? GetOldest(int accountId)
    {
        throw new NotImplementedException();
    }

    public StockAccountEntry? GetYoungest(int accountId)
    {
        throw new NotImplementedException();
    }

    public bool Update(StockAccountEntry entry)
    {
        throw new NotImplementedException();
    }
}
