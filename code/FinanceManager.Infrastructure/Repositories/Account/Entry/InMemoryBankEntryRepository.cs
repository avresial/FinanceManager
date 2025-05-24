using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryBankEntryRepository : IAccountEntryRepository<BankAccountEntry>
{
    private readonly BankAccountEntryContext _context;

    public InMemoryBankEntryRepository(BankAccountEntryContext context)
    {
        _context = context;
    }

    public bool Add(BankAccountEntry entry)
    {
        BankAccountEntry newBankAccountEntry = new(entry.AccountId, GetHighestEntry() + 1, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            ExpenseType = entry.ExpenseType
        };

        _context.Entries.Add(newBankAccountEntry);
        _context.SaveChanges();

        RecalculateValues(newBankAccountEntry.AccountId, newBankAccountEntry.EntryId);
        return true;
    }

    public bool Delete(int accountId, int entryId)
    {
        var entryToDelete = _context.Entries.FirstOrDefault(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entryToDelete == null) return false;
        _context.Entries.Remove(entryToDelete);
        _context.SaveChanges();
        RecalculateValues(entryToDelete.AccountId, entryToDelete.PostingDate);
        return true;
    }

    public bool Delete(int accountId)
    {
        var entriesToRemove = _context.Entries.Where(e => e.AccountId == accountId).ToList();
        _context.Entries.RemoveRange(entriesToRemove);
        _context.SaveChanges();
        return true;
    }

    public IEnumerable<BankAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) => _context.Entries
            .Where(x => x.AccountId == accountId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId);

    public int? GetCount(int accountId) => _context.Entries.Count(x => x.AccountId == accountId);

    public BankAccountEntry? GetNextOlder(int accountId, int entryId)
    {
        var existingEntry = _context.Entries.FirstOrDefault(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return _context.Entries
            .Where(x => x.AccountId == accountId && x.PostingDate < existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefault();
    }

    public BankAccountEntry? GetNextOlder(int accountId, DateTime date) => _context.Entries
             .Where(x => x.AccountId == accountId && x.PostingDate < date)
             .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
             .FirstOrDefault();

    public BankAccountEntry? GetNextYounger(int accountId, int entryId)
    {
        var existingEntry = _context.Entries.FirstOrDefault(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return _context.Entries
            .Where(x => x.AccountId == accountId && x.PostingDate > existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefault();
    }

    public BankAccountEntry? GetNextYounger(int accountId, DateTime date) => _context.Entries
            .Where(x => x.AccountId == accountId && x.PostingDate > date)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefault();

    public BankAccountEntry? GetOldest(int accountId) => _context.Entries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefault();

    public BankAccountEntry? GetYoungest(int accountId) => _context.Entries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefault();

    public bool Update(BankAccountEntry entry)
    {
        var existingEntry = _context.Entries.FirstOrDefault(e => e.AccountId == entry.AccountId && e.EntryId == entry.EntryId);
        if (existingEntry is null) return false;

        existingEntry.Update(entry);
        _context.SaveChanges();
        RecalculateValues(entry.AccountId, entry.EntryId);
        return true;
    }

    private void RecalculateValues(int accountId, int entryId)
    {
        var entry = _context.Entries.FirstOrDefault(e => e.AccountId == accountId && e.EntryId == entryId);
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
        _context.SaveChanges();
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
        _context.SaveChanges();
    }

    private int GetHighestEntry() => _context.Entries
            .ToList()
            .Select(x => x.EntryId)
            .DefaultIfEmpty(0)
            .Max();
}
