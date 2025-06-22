using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryStockEntryRepository(AppDbContext context) : IStockAccountEntryRepository<StockAccountEntry>
{
    private readonly AppDbContext _dbContext = context;

    public async Task<bool> Add(StockAccountEntry entry)
    {
        StockAccountEntry newBankAccountEntry = new(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange, entry.Ticker, entry.InvestmentType);

        _dbContext.StockEntries.Add(newBankAccountEntry);
        await _dbContext.SaveChangesAsync();

        await RecalculateValues(newBankAccountEntry.AccountId, newBankAccountEntry.EntryId);

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
    public async Task<IEnumerable<StockAccountEntry>> Get(int accountId, string ticker, DateTime startDate, DateTime endDate)
    {
        return await _dbContext.StockEntries
            .Where(e => e.AccountId == accountId && e.Ticker == ticker && e.PostingDate >= startDate && e.PostingDate <= endDate)
            .ToListAsync();
    }

    public Task<StockAccountEntry?> Get(int accountId, int entryId)
    {
        throw new NotImplementedException();
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

    async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextOlder(int accountId, DateTime date)
    {
        Dictionary<string, StockAccountEntry> result = [];

        var tickers = await _dbContext.StockEntries.Select(m => m.Ticker).Distinct().ToListAsync();

        foreach (var ticker in tickers)
        {
            var nextOlder = await _dbContext.StockEntries
                   .Where(e => e.Ticker == ticker && e.AccountId == accountId && e.PostingDate < date)
                   .OrderByDescending(e => e.PostingDate)
                   .FirstOrDefaultAsync();

            if (nextOlder is null) continue;

            result.Add(ticker, nextOlder);
        }

        return result;
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


    async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextYounger(int accountId, DateTime date)
    {
        Dictionary<string, StockAccountEntry> result = [];
        var tickers = await _dbContext.StockEntries.Select(m => m.Ticker).Distinct().ToListAsync();

        foreach (var ticker in tickers)
        {
            var nextOlder = await _dbContext.StockEntries
                   .Where(e => e.Ticker == ticker && e.AccountId == accountId && e.PostingDate > date)
                   .OrderBy(e => e.PostingDate)
                   .FirstOrDefaultAsync();

            if (nextOlder is null) continue;

            result.Add(ticker, nextOlder);
        }

        return result;
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



    private async Task RecalculateValues(int accountId, int entryId)
    {
        var entry = await _dbContext.StockEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return;

        var entriesToUpdate = await Get(accountId, entry.Ticker, entry.PostingDate, DateTime.UtcNow);
        var previousEntries = await ((IStockAccountEntryRepository<StockAccountEntry>)this).GetNextOlder(accountId, entry.PostingDate);

        StockAccountEntry? previousEntry = previousEntries.ContainsKey(entry.Ticker) ? previousEntries[entry.Ticker] : null;

        foreach (var entryToUpdate in entriesToUpdate.OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId))
        {
            if (previousEntry is not null)
                entryToUpdate.Value = previousEntry.Value + entryToUpdate.ValueChange;
            else
                entryToUpdate.Value = entryToUpdate.ValueChange;

            previousEntry = entryToUpdate;
        }

        _dbContext.SaveChanges();
    }
}
