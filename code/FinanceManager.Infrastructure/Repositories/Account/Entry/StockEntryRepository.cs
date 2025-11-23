using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class StockEntryRepository(AppDbContext context) : IStockAccountEntryRepository<StockAccountEntry>
{
    public async Task<bool> Add(StockAccountEntry entry, bool recalculate = true)
    {
        StockAccountEntry newBankAccountEntry = new(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange, entry.Ticker, entry.InvestmentType);

        context.StockEntries.Add(newBankAccountEntry);
        await context.SaveChangesAsync();

        if (recalculate)
            await RecalculateValues(newBankAccountEntry.AccountId, newBankAccountEntry.EntryId);

        return true;
    }

    public async Task<bool> Delete(int accountId, int entryId)
    {
        var entry = await context.StockEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return false;

        var nextYounger = await GetNextYounger(accountId, entryId);

        context.StockEntries.Remove(entry);
        await context.SaveChangesAsync();

        if (nextYounger is not null)
            await RecalculateValues(nextYounger.AccountId, nextYounger.EntryId);

        return true;
    }
    public async Task<bool> Delete(int accountId)
    {
        var entries = await context.StockEntries.Where(e => e.AccountId == accountId).ToListAsync();
        context.StockEntries.RemoveRange(entries);
        await context.SaveChangesAsync();
        return true;
    }
    public IAsyncEnumerable<StockAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) => context.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate >= startDate && e.PostingDate <= endDate)
            .AsAsyncEnumerable();

    public IAsyncEnumerable<StockAccountEntry> Get(int accountId, string ticker, DateTime startDate, DateTime endDate) => context.StockEntries
            .Where(e => e.AccountId == accountId && e.Ticker == ticker && e.PostingDate >= startDate && e.PostingDate <= endDate)
            .AsAsyncEnumerable();


    public Task<StockAccountEntry?> Get(int accountId, int entryId) =>
        context.StockEntries.SingleOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
    public Task<int> GetCount(int accountId) => context.StockEntries.CountAsync(e => e.AccountId == accountId);

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var entry = await context.StockEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return null;

        return await context.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate < entry.PostingDate)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, DateTime date)
    {
        return await context.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate < date)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextOlder(int accountId, DateTime date)
    {
        Dictionary<string, StockAccountEntry> result = [];

        var tickers = await context.StockEntries
                                .Where(e => e.AccountId == accountId)
                                .Select(m => m.Ticker)
                                .Distinct()
                                .ToListAsync();

        foreach (var ticker in tickers)
        {
            var nextOlder = await context.StockEntries
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
        var entry = await context.StockEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return null;
        return await context.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate > entry.PostingDate)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public Task<StockAccountEntry?> GetNextYounger(int accountId, DateTime date) => context.StockEntries
            .Where(e => e.AccountId == accountId && e.PostingDate > date)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();


    async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextYounger(int accountId, DateTime date)
    {
        Dictionary<string, StockAccountEntry> result = [];
        var tickers = await context.StockEntries.Select(m => m.Ticker).Distinct().ToListAsync();

        foreach (var ticker in tickers)
        {
            var nextOlder = await context.StockEntries
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
        return await context.StockEntries
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }
    public async Task<StockAccountEntry?> GetYoungest(int accountId)
    {
        return await context.StockEntries
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> Update(StockAccountEntry entry)
    {
        var entryToUpdate = await context.StockEntries.FirstOrDefaultAsync(e => e.AccountId == entry.AccountId && e.EntryId == entry.EntryId);
        if (entryToUpdate is null) return false;
        entryToUpdate.Update(entry);
        await context.SaveChangesAsync();

        await RecalculateValues(entry.AccountId, entry.EntryId);
        return true;
    }

    public async Task RecalculateValues(int accountId, int entryId)
    {
        var entry = await context.StockEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return;

        var previousEntries = await ((IStockAccountEntryRepository<StockAccountEntry>)this).GetNextOlder(accountId, entry.PostingDate);

        StockAccountEntry? previousEntry = previousEntries.ContainsKey(entry.Ticker) ? previousEntries[entry.Ticker] : null;

        await foreach (var entryToUpdate in Get(accountId, entry.Ticker, entry.PostingDate, DateTime.UtcNow).OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId))
        {
            if (previousEntry is not null)
                entryToUpdate.Value = previousEntry.Value + entryToUpdate.ValueChange;
            else
                entryToUpdate.Value = entryToUpdate.ValueChange;

            previousEntry = entryToUpdate;
        }

        await context.SaveChangesAsync();
    }
    public async Task<bool> AddLabel(int entryId, int labelId)
    {
        var entry = await context.StockEntries.FirstOrDefaultAsync(e => e.EntryId == entryId);
        var label = await context.FinancialLabels.FirstOrDefaultAsync(l => l.Id == labelId);

        if (entry is null || label is null) return false;

        //if (entry.LabelBankEntries.Any(l => l.FinancialLabelId == labelId)) return false;


        await context.SaveChangesAsync();

        return true;

    }
}
