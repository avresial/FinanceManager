using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryStockEntryRepository : IAccountEntryRepository<StockAccountEntry>
{
    private List<StockAccount> _accounts = [];
    public async Task<bool> Add(StockAccountEntry entry)
    {
        StockAccount? stockAccount = _accounts.FirstOrDefault(x => x.AccountId == entry.AccountId);
        if (stockAccount is null)
        {
            stockAccount = new StockAccount(1, entry.AccountId, "");
            _accounts.Add(stockAccount);
        }

        stockAccount.Add(entry);

        return true;
    }

    public async Task<bool> Delete(int accountId, int entryId)
    {
        var stockAccount = _accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (stockAccount is null) return false;
        stockAccount.Remove(entryId);
        return true;
    }
    public async Task<bool> Delete(int accountId)
    {
        _accounts.RemoveAll(x => x.AccountId == accountId);
        return true;
    }
    public async Task<IEnumerable<StockAccountEntry>> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var stockAccount = _accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (stockAccount is null) return [];
        return stockAccount.Get(startDate, endDate);
    }

    public async Task<int?> GetCount(int accountId)
    {
        var account = _accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (account is null || account.Entries is null) return null;

        return account.Entries.Count;
    }

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var account = _accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (account is null || account.Entries is null) return null;

        var entry = account.Entries.FirstOrDefault(x => x.EntryId == entryId);
        if (entry is null) return null;

        return account.Entries
            .Where(x => x.PostingDate < entry.PostingDate)
            .OrderByDescending(x => x.PostingDate)
            .FirstOrDefault();
    }

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, DateTime date)
    {
        throw new NotImplementedException();
    }

    public async Task<StockAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var account = _accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (account is null || account.Entries is null) return null;

        var entry = account.Entries.FirstOrDefault(x => x.EntryId == entryId);
        if (entry is null) return null;

        return account.Entries
            .Where(x => x.PostingDate > entry.PostingDate)
            .LastOrDefault();
    }

    public async Task<StockAccountEntry?> GetNextYounger(int accountId, DateTime date)
    {
        throw new NotImplementedException();
    }

    public async Task<StockAccountEntry?> GetOldest(int accountId)
    {
        var stockAccount = _accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (stockAccount is null) return null;
        if (stockAccount.Entries is null || !stockAccount.Entries.Any()) return null;
        var maxDate = stockAccount.Entries.Min(x => x.PostingDate);

        return stockAccount.Entries.First(x => x.PostingDate == maxDate);
    }
    public async Task<StockAccountEntry?> GetYoungest(int accountId)
    {
        var stockAccount = _accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (stockAccount is null) return null;
        if (stockAccount.Entries is null || !stockAccount.Entries.Any()) return null;
        var minDate = stockAccount.Entries.Max(x => x.PostingDate);

        return stockAccount.Entries.First(x => x.PostingDate == minDate);
    }
    public async Task<bool> Update(StockAccountEntry entry)
    {
        var stockAccount = _accounts.FirstOrDefault(x => x.AccountId == entry.AccountId);
        if (stockAccount is null) return false;
        if (stockAccount.Entries is null || !stockAccount.Entries.Any()) return false;

        var entryToUpdate = stockAccount.Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);

        if (entryToUpdate is null) return false;

        entryToUpdate.Update(entry);
        return true;
    }
}
