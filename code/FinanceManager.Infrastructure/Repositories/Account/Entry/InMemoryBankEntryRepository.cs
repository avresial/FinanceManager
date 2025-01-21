using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;
public class InMemoryBankEntryRepository : IAccountEntryRepository<BankAccountEntry>
{
    private List<BankAccountEntry> _entries = [];
    public bool Add(BankAccountEntry entry)
    {
        var newEntry = new BankAccountEntry(entry.AccountId, entry.EntryId, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            ExpenseType = entry.ExpenseType,
        };

        _entries.Add(newEntry);
        return true;
    }

    public bool Delete(int accountId, int entryId)
    {
        if (!_entries.Any(x => x.AccountId == accountId && x.EntryId == entryId)) return false;

        _entries.RemoveAll(x => x.AccountId == accountId && x.EntryId == entryId);

        return true;
    }

    public IEnumerable<BankAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) =>
        _entries.Where(x => x.AccountId == accountId && x.PostingDate > startDate && x.PostingDate < endDate);

    public BankAccountEntry? GetOldest(int accountId)
    {
        if (!_entries.Any()) return null;
        var maxDate = _entries.Max(x => x.PostingDate);

        return _entries.First(x => x.PostingDate == maxDate);
    }

    public BankAccountEntry? GetYoungest(int accountId)
    {
        if (!_entries.Any()) return null;
        var minDate = _entries.Min(x => x.PostingDate);

        return _entries.First(x => x.PostingDate == minDate);
    }

    public bool Update(BankAccountEntry entry)
    {
        var entryToUpdate = _entries.FirstOrDefault(x => x.AccountId == entry.AccountId && x.EntryId == entry.EntryId);
        if (entryToUpdate is null) return false;

        entryToUpdate.Update(entry);
        return true;
    }
}
