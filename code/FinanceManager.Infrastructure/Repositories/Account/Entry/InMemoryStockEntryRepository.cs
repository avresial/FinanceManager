using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryStockEntryRepository : IAccountEntryRepository<StockAccountEntry>
{
    private List<StockAccount> accounts = [];
    public bool Add(StockAccountEntry entry)
    {
        StockAccount? stockAccount = accounts.FirstOrDefault(x => x.AccountId == entry.AccountId);
        if (stockAccount is null)
        {
            stockAccount = new StockAccount(1, entry.AccountId, "");
            accounts.Add(stockAccount);
        }

        stockAccount.Add(entry);

        return true;
    }

    public bool Delete(int accountId, int entryId)
    {
        var stockAccount = accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (stockAccount is null) return false;
        stockAccount.Remove(entryId);
        return true;
    }
    public IEnumerable<StockAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var stockAccount = accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (stockAccount is null) return [];
        return stockAccount.Get(startDate, endDate);
    }
    public StockAccountEntry? GetOldest(int accountId)
    {
        var stockAccount = accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (stockAccount is null) return null;
        if (stockAccount.Entries is null || !stockAccount.Entries.Any()) return null;
        var maxDate = stockAccount.Entries.Min(x => x.PostingDate);

        return stockAccount.Entries.First(x => x.PostingDate == maxDate);
    }
    public StockAccountEntry? GetYoungest(int accountId)
    {
        var stockAccount = accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (stockAccount is null) return null;
        if (stockAccount.Entries is null || !stockAccount.Entries.Any()) return null;
        var minDate = stockAccount.Entries.Max(x => x.PostingDate);

        return stockAccount.Entries.First(x => x.PostingDate == minDate);
    }
    public bool Update(StockAccountEntry entry)
    {
        var stockAccount = accounts.FirstOrDefault(x => x.AccountId == entry.AccountId);
        if (stockAccount is null) return false;
        if (stockAccount.Entries is null || !stockAccount.Entries.Any()) return false;

        var entryToUpdate = stockAccount.Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);

        if (entryToUpdate is null) return false;

        entryToUpdate.Update(entry);
        return true;
    }
}
