using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using System.Collections.Concurrent;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryBankEntryRepository : IAccountEntryRepository<BankAccountEntry>
{
    private readonly ConcurrentDictionary<(int, int), BankAccountEntry> _entries = new();

    public bool Add(BankAccountEntry entry)
    {
        BankAccountEntry newBankAccountEntry = new(entry.AccountId, GetHighestEntry() + 1, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            ExpenseType = entry.ExpenseType
        };

        _entries.TryAdd((entry.AccountId, newBankAccountEntry.EntryId), newBankAccountEntry);


        RecalculateValues(newBankAccountEntry.AccountId, newBankAccountEntry.EntryId);
        return true;
    }

    public bool Delete(int accountId, int entryId)
    {
        _entries.TryGetValue((accountId, entryId), out BankAccountEntry? entryToDelete);

        if (entryToDelete == null) return false;

        var result = _entries.TryRemove((accountId, entryId), out BankAccountEntry? _);
        if (!result) return false;
        RecalculateValues(entryToDelete.AccountId, entryToDelete.PostingDate);
        return true;
    }

    public bool Delete(int accountId)
    {
        foreach (var key in _entries.Keys.Where(x => x.Item1 == accountId))
            _entries.TryRemove(key, out BankAccountEntry? _);
        return true;
    }

    public IEnumerable<BankAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) => _entries
            .Where(x => x.Key.Item1 == accountId && x.Value.PostingDate >= startDate && x.Value.PostingDate <= endDate)
            .Select(x => x.Value)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId);

    public int? GetCount(int accountId) => _entries.Count(x => x.Key.Item1 == accountId);

    public BankAccountEntry? GetNextOlder(int accountId, int entryId)
    {
        _entries.TryGetValue((accountId, entryId), out BankAccountEntry? existingEntry);
        if (existingEntry is null) return default;

        return _entries
            .Where(x => x.Key.Item1 == accountId && x.Value.PostingDate < existingEntry.PostingDate)
            .OrderByDescending(x => x.Value.PostingDate).ThenByDescending(x => x.Value.EntryId)
            .FirstOrDefault().Value;
    }

    public BankAccountEntry? GetNextOlder(int accountId, DateTime date) => _entries
             .Where(x => x.Value.AccountId == accountId && x.Value.PostingDate < date)
             .OrderByDescending(x => x.Value.PostingDate).ThenByDescending(x => x.Value.EntryId)
             .FirstOrDefault().Value;

    public BankAccountEntry? GetNextYounger(int accountId, int entryId)
    {
        _entries.TryGetValue((accountId, entryId), out BankAccountEntry? existingEntry);
        if (existingEntry is null) return default;

        return _entries
            .Where(x => x.Value.AccountId == accountId && x.Value.PostingDate > existingEntry.PostingDate)
            .OrderByDescending(x => x.Value.PostingDate).ThenByDescending(x => x.Value.EntryId)
            .LastOrDefault().Value;

    }

    public BankAccountEntry? GetNextYounger(int accountId, DateTime date) => _entries
            .Where(x => x.Value.AccountId == accountId && x.Value.PostingDate > date)
            .OrderByDescending(x => x.Value.PostingDate).ThenByDescending(x => x.Value.EntryId)
            .LastOrDefault().Value;

    public BankAccountEntry? GetOldest(int accountId) => _entries
            .Where(x => x.Value.AccountId == accountId)
            .OrderByDescending(x => x.Value.PostingDate).ThenByDescending(x => x.Value.EntryId)
            .LastOrDefault().Value;

    public BankAccountEntry? GetYoungest(int accountId) => _entries
            .Where(x => x.Value.AccountId == accountId)
            .OrderByDescending(x => x.Value.PostingDate).ThenByDescending(x => x.Value.EntryId)
            .FirstOrDefault().Value;

    public bool Update(BankAccountEntry entry)
    {
        _entries.TryGetValue((entry.AccountId, entry.EntryId), out BankAccountEntry? existingEntry);
        if (existingEntry is null) return default;

        existingEntry.Update(entry);
        RecalculateValues(entry.AccountId, entry.EntryId);
        return true;
    }

    private void RecalculateValues(int accountId, int entryId)
    {
        _entries.TryGetValue((accountId, entryId), out BankAccountEntry? entry);
        if (entry is null) return;

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

    private int GetHighestEntry() => _entries
            .Select(x => x.Value.EntryId)
            .DefaultIfEmpty(0)
            .Max();
}
