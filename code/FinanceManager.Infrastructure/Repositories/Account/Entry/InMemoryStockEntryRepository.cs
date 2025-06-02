using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryStockEntryRepository(AppDbContext context) : IAccountEntryRepository<StockAccountEntry>
{
    private readonly AppDbContext _dbContext = context;

    public async Task<bool> Add(StockAccountEntry entry)
    {
        await _dbContext.StockEntries.AddAsync(entry);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> Delete(int accountId, int entryId)
    {
        var entry = await _dbContext.StockEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return false;
        _dbContext.StockEntries.Remove(entry);
        await _dbContext.SaveChangesAsync();
        return true;
    }
    public async Task<bool> Delete(int accountId)
    {
        var entries = await _dbContext.StockEntries.Where(e => e.AccountId == accountId).ToListAsync();
        _dbContext.StockEntries.RemoveRange(entries);
        await _dbContext.SaveChangesAsync();
        return true;
    }
    public async Task<IEnumerable<StockAccountEntry>> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        return await _dbContext.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate >= startDate && e.PostingDate <= endDate)
            .ToListAsync();
    }

    public async Task<int?> GetCount(int accountId)
    {
        return await _dbContext.StockEntries.CountAsync(e => e.AccountId == accountId);
    }

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var entry = await _dbContext.StockEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return null;
        return await _dbContext.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate < entry.PostingDate)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, DateTime date)
    {
        return await _dbContext.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate < date)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var entry = await _dbContext.StockEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return null;
        return await _dbContext.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate > entry.PostingDate)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetNextYounger(int accountId, DateTime date)
    {
        return await _dbContext.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate > date)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetOldest(int accountId)
    {
        return await _dbContext.StockEntries
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }
    public async Task<StockAccountEntry?> GetYoungest(int accountId)
    {
        return await _dbContext.StockEntries
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> Update(StockAccountEntry entry)
    {
        var entryToUpdate = await _dbContext.StockEntries.FirstOrDefaultAsync(e => e.AccountId == entry.AccountId && e.EntryId == entry.EntryId);
        if (entryToUpdate is null) return false;
        entryToUpdate.Update(entry);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}
