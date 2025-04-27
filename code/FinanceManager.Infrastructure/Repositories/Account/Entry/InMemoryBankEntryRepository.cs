using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryBankEntryRepository : IAccountEntryRepository<BankAccountEntry>
{
    private readonly List<BankAccountEntry> _entries = [];

    public bool Add(BankAccountEntry entry)
    {
        BankAccountEntry newBankAccountEntry = new(entry.AccountId, GetHighestEntry() + 1, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            ExpenseType = entry.ExpenseType
        };

        _entries.Add(newBankAccountEntry);

        RecalculateValues(newBankAccountEntry.AccountId, newBankAccountEntry.EntryId);
        return true;
    }

    public bool Delete(int accountId, int entryId)
    {
        var entryToDelete = _entries.FirstOrDefault(x => x.AccountId == accountId && x.EntryId == entryId);
        if (entryToDelete == null) return false;

        _entries.Remove(entryToDelete);
        RecalculateValues(entryToDelete.AccountId, entryToDelete.PostingDate);
        return true;
    }

    public bool Delete(int accountId)
    {
        _entries.RemoveAll(x => x.AccountId == accountId);
        return true;
    }

    public IEnumerable<BankAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        return _entries
            .Where(x => x.AccountId == accountId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId);
    }

    public int? GetCount(int accountId)
    {
        return _entries.Count(x => x.AccountId == accountId);
    }

    public BankAccountEntry? GetNextOlder(int accountId, int entryId)
    {
        var existingEntry = _entries.FirstOrDefault(x => x.AccountId == accountId && x.EntryId == entryId);
        if (existingEntry == null) return default;

        return _entries
            .Where(x => x.AccountId == accountId && x.PostingDate < existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefault();
    }

    public BankAccountEntry? GetNextOlder(int accountId, DateTime date)
    {
        return _entries
             .Where(x => x.AccountId == accountId && x.PostingDate < date)
             .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
             .FirstOrDefault();
    }

    public BankAccountEntry? GetNextYounger(int accountId, int entryId)
    {
        var existingEntry = _entries.FirstOrDefault(x => x.AccountId == accountId && x.EntryId == entryId);
        if (existingEntry == null) return default;

        return _entries
            .Where(x => x.AccountId == accountId && x.PostingDate > existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefault();

    }

    public BankAccountEntry? GetNextYounger(int accountId, DateTime date)
    {
        return _entries
            .Where(x => x.AccountId == accountId && x.PostingDate > date)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefault();
    }

    public BankAccountEntry? GetOldest(int accountId)
    {
        return _entries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefault();
    }

    public BankAccountEntry? GetYoungest(int accountId)
    {
        return _entries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefault();
    }

    public bool Update(BankAccountEntry entry)
    {
        var existingEntry = _entries.FirstOrDefault(x => x.AccountId == entry.AccountId && x.EntryId == entry.EntryId);
        if (existingEntry == null) return false;

        existingEntry.Update(entry);
        RecalculateValues(entry.AccountId, entry.EntryId);
        return true;
    }

    private void RecalculateValues(int accountId, int entryId)
    {
        var entry = _entries.FirstOrDefault(x => x.AccountId == accountId && x.EntryId == entryId);
        if (entry == null) return;
        var entriesToUpdate = Get(accountId, entry.PostingDate, DateTime.UtcNow);
        BankAccountEntry? previousEntry = GetNextOlder(accountId, entry.PostingDate);

        foreach (var entryToUpdate in entriesToUpdate.OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId))
        {
            if (previousEntry is not null)
                entryToUpdate.Value = previousEntry.Value + entryToUpdate.ValueChange;
            else
                entryToUpdate.Value = entryToUpdate.ValueChange;

            previousEntry = entryToUpdate;
        }
    }

    private void RecalculateValues(int accountId, DateTime startDate)
    {
        var entriesToUpdate = Get(accountId, startDate, DateTime.UtcNow);
        BankAccountEntry? previousEntry = GetNextOlder(accountId, startDate);

        foreach (var entryToUpdate in entriesToUpdate.OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId))
        {
            if (previousEntry is not null)
                entryToUpdate.Value = previousEntry.Value + entryToUpdate.ValueChange;
            else
                entryToUpdate.Value = entryToUpdate.ValueChange;

            previousEntry = entryToUpdate;
        }
    }

    private int GetHighestEntry()
    {
        return _entries
            .Select(x => x.EntryId)
            .DefaultIfEmpty(0)
            .Max();
    }
}
