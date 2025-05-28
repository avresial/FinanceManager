using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryBankEntryRepository : IAccountEntryRepository<BankAccountEntry>
{
    private readonly BankAccountEntryContext _context;

    public InMemoryBankEntryRepository(BankAccountEntryContext context)
    {
        _context = context;
    }

    public async Task<bool> Add(BankAccountEntry entry)
    {
        BankAccountEntry newBankAccountEntry = new(entry.AccountId, GetHighestEntry() + 1, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            ExpenseType = entry.ExpenseType
        };

        _context.Entries.Add(newBankAccountEntry);
        await _context.SaveChangesAsync();

        await RecalculateValues(newBankAccountEntry.AccountId, newBankAccountEntry.EntryId);
        return true;
    }

    public async Task<bool> Delete(int accountId, int entryId)
    {
        var entryToDelete = await _context.Entries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entryToDelete == null) return false;
        _context.Entries.Remove(entryToDelete);
        await _context.SaveChangesAsync();
        await RecalculateValues(entryToDelete.AccountId, entryToDelete.PostingDate);
        return true;
    }

    public async Task<bool> Delete(int accountId)
    {
        var entriesToRemove = await _context.Entries.Where(e => e.AccountId == accountId).ToListAsync();
        _context.Entries.RemoveRange(entriesToRemove);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<BankAccountEntry>> Get(int accountId, DateTime startDate, DateTime endDate) => await _context.Entries
            .Where(x => x.AccountId == accountId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId).ToListAsync();

    public async Task<int?> GetCount(int accountId) => await _context.Entries.CountAsync(x => x.AccountId == accountId);

    public async Task<BankAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var existingEntry = await _context.Entries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await _context.Entries
            .Where(x => x.AccountId == accountId && x.PostingDate < existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();
    }

    public async Task<BankAccountEntry?> GetNextOlder(int accountId, DateTime date) => await _context.Entries
             .Where(x => x.AccountId == accountId && x.PostingDate < date)
             .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
             .FirstOrDefaultAsync();

    public async Task<BankAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var existingEntry = await _context.Entries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await _context.Entries
            .Where(x => x.AccountId == accountId && x.PostingDate > existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();
    }

    public async Task<BankAccountEntry?> GetNextYounger(int accountId, DateTime date) => await _context.Entries
            .Where(x => x.AccountId == accountId && x.PostingDate > date)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<BankAccountEntry?> GetOldest(int accountId) => await _context.Entries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<BankAccountEntry?> GetYoungest(int accountId) => await _context.Entries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();

    public async Task<bool> Update(BankAccountEntry entry)
    {
        var existingEntry = await _context.Entries.FirstOrDefaultAsync(e => e.AccountId == entry.AccountId && e.EntryId == entry.EntryId);
        if (existingEntry is null) return false;

        existingEntry.Update(entry);
        await _context.SaveChangesAsync();
        await RecalculateValues(entry.AccountId, entry.EntryId);
        return true;
    }

    private async Task RecalculateValues(int accountId, int entryId)
    {
        var entry = await _context.Entries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return;

        var entriesToUpdate = await Get(accountId, entry.PostingDate, DateTime.UtcNow);
        BankAccountEntry? previousEntry = await GetNextOlder(accountId, entry.PostingDate);

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

    private async Task RecalculateValues(int accountId, DateTime startDate)
    {
        var entriesToUpdate = await Get(accountId, startDate, DateTime.UtcNow);
        BankAccountEntry? previousEntry = await GetNextOlder(accountId, startDate);

        foreach (var entryToUpdate in entriesToUpdate.OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId))
        {
            if (previousEntry is not null)
                entryToUpdate.Value = previousEntry.Value + entryToUpdate.ValueChange;
            else
                entryToUpdate.Value = entryToUpdate.ValueChange;

            previousEntry = entryToUpdate;
        }
        await _context.SaveChangesAsync();
    }

    private int GetHighestEntry() => _context.Entries
            .ToList()
            .Select(x => x.EntryId)
            .DefaultIfEmpty(0)
            .Max();
}
