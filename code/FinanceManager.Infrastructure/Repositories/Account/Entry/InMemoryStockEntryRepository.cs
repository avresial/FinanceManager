using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryStockEntryRepository : IAccountEntryRepository<StockAccountEntry>
{
    private List<StockAccountEntry> _entries = [];
    public bool Add(StockAccountEntry entry)
    {
        var newEntry = new StockAccountEntry(entry.AccountId, entry.EntryId, entry.PostingDate,
            entry.Value, entry.ValueChange, entry.Ticker, entry.InvestmentType);

        _entries.Add(newEntry);
        return true;
    }

    public bool Delete(int accountId, int entryId)
    {
        if (!_entries.Any(x => x.AccountId == accountId && x.EntryId == entryId)) return false;

        _entries.RemoveAll(x => x.AccountId == accountId && x.EntryId == entryId);

        return true;
    }

    public IEnumerable<StockAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) =>
        _entries.Where(x => x.AccountId == accountId && x.PostingDate > startDate && x.PostingDate < endDate);

    public StockAccountEntry? GetOldest(int accountId)
    {
        if (!_entries.Any()) return null;
        var maxDate = _entries.Max(x => x.PostingDate);

        return _entries.First(x => x.PostingDate == maxDate);
    }

    public StockAccountEntry? GetYoungest(int accountId)
    {
        if (!_entries.Any()) return null;
        var minDate = _entries.Min(x => x.PostingDate);

        return _entries.First(x => x.PostingDate == minDate);
    }

    public bool Update(StockAccountEntry entry)
    {
        var entryToUpdate = _entries.FirstOrDefault(x => x.AccountId == entry.AccountId && x.EntryId == entry.EntryId);
        if (entryToUpdate is null) return false;

        entryToUpdate.Update(entry);
        return true;
    }
}
