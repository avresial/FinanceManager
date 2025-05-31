using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryStockEntryRepository(StockAccountEntryContext stockAccountEntryContext) : IAccountEntryRepository<StockAccountEntry>
{
    private readonly StockAccountEntryContext _context = stockAccountEntryContext;

    public async Task<bool> Add(StockAccountEntry entry)
    {
        await _context.Entries.AddAsync(entry);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> Delete(int accountId, int entryId)
    {
        var entry = await _context.Entries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return false;
        _context.Entries.Remove(entry);
        await _context.SaveChangesAsync();
        return true;
    }
    public async Task<bool> Delete(int accountId)
    {
        var entries = await _context.Entries.Where(e => e.AccountId == accountId).ToListAsync();
        _context.Entries.RemoveRange(entries);
        await _context.SaveChangesAsync();
        return true;
    }
    public async Task<IEnumerable<StockAccountEntry>> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        return await _context.Entries
            .Where(e => e.AccountId == accountId && e.PostingDate >= startDate && e.PostingDate <= endDate)
            .ToListAsync();
    }

    public async Task<int?> GetCount(int accountId)
    {
        return await _context.Entries.CountAsync(e => e.AccountId == accountId);
    }

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var entry = await _context.Entries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return null;
        return await _context.Entries
            .Where(e => e.AccountId == accountId && e.PostingDate < entry.PostingDate)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, DateTime date)
    {
        return await _context.Entries
            .Where(e => e.AccountId == accountId && e.PostingDate < date)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var entry = await _context.Entries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return null;
        return await _context.Entries
            .Where(e => e.AccountId == accountId && e.PostingDate > entry.PostingDate)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetNextYounger(int accountId, DateTime date)
    {
        return await _context.Entries
            .Where(e => e.AccountId == accountId && e.PostingDate > date)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetOldest(int accountId)
    {
        return await _context.Entries
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }
    public async Task<StockAccountEntry?> GetYoungest(int accountId)
    {
        return await _context.Entries
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> Update(StockAccountEntry entry)
    {
        var entryToUpdate = await _context.Entries.FirstOrDefaultAsync(e => e.AccountId == entry.AccountId && e.EntryId == entry.EntryId);
        if (entryToUpdate is null) return false;
        entryToUpdate.Update(entry);
        await _context.SaveChangesAsync();
        return true;
    }
}
