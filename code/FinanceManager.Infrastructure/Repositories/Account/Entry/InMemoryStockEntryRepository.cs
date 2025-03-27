using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryStockEntryRepository : IAccountEntryRepository<StockAccountEntry>
{
    private List<StockAccount> accounts = [];
    public bool Add(StockAccountEntry entry)
    {
        StockAccount? bankAccount = accounts.FirstOrDefault(x => x.AccountId == entry.AccountId);
        if (bankAccount is null)
        {
            bankAccount = new StockAccount(1, entry.AccountId, "");
            accounts.Add(bankAccount);
        }

        bankAccount.Add(entry);

        return true;
    }

    public bool Delete(int accountId, int entryId)
    {
        var bankAccount = accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount is null) return false;
        bankAccount.Remove(entryId);
        return true;
    }
    public IEnumerable<StockAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var bankAccount = accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount is null) return [];
        return bankAccount.Get(startDate, endDate);
    }
    public StockAccountEntry? GetOldest(int accountId)
    {
        var bankAccount = accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount is null) return null;
        if (bankAccount.Entries is null || !bankAccount.Entries.Any()) return null;
        var maxDate = bankAccount.Entries.Min(x => x.PostingDate);

        return bankAccount.Entries.First(x => x.PostingDate == maxDate);
    }
    public StockAccountEntry? GetYoungest(int accountId)
    {
        var bankAccount = accounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount is null) return null;
        if (bankAccount.Entries is null || !bankAccount.Entries.Any()) return null;
        var minDate = bankAccount.Entries.Max(x => x.PostingDate);

        return bankAccount.Entries.First(x => x.PostingDate == minDate);
    }
    public bool Update(StockAccountEntry entry)
    {
        var bankAccount = accounts.FirstOrDefault(x => x.AccountId == entry.AccountId);
        if (bankAccount is null) return false;
        if (bankAccount.Entries is null || !bankAccount.Entries.Any()) return false;

        var entryToUpdate = bankAccount.Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);

        if (entryToUpdate is null) return false;

        entryToUpdate.Update(entry);
        return true;
    }
}
