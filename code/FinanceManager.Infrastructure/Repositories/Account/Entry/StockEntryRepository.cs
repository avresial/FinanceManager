using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class StockEntryRepository(AppDbContext context) : IStockAccountEntryRepository<StockAccountEntry>
{
    public async Task<bool> Add(StockAccountEntry entry, bool recalculate = true)
    {
        StockAccountEntry newEntry = new(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange, entry.Ticker, entry.InvestmentType);

        context.StockEntries.Add(newEntry);
        await context.SaveChangesAsync();

        if (recalculate)
            await RecalculateValues(newEntry.AccountId, newEntry.EntryId);

        return true;
    }
    public async Task<bool> Add(IEnumerable<StockAccountEntry> entries, bool recalculate = true)
    {
        StockAccountEntry? firstEntry = null;

        foreach (var entry in entries)
        {
            var newEntry = new StockAccountEntry(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange, entry.Ticker, entry.InvestmentType)
            {
                Labels = entry.Labels,
            };

            if (firstEntry is null) firstEntry = newEntry;
            context.StockEntries.Add(newEntry);
        }

        await context.SaveChangesAsync();
        if (recalculate && firstEntry is not null)
            await RecalculateValues(firstEntry.AccountId, firstEntry.EntryId);

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

        await context.SaveChangesAsync();

        return true;
    }

    public async Task<int> AddLabels(IEnumerable<(int entryId, int labelId)> labelAssignments, CancellationToken cancellationToken = default)
    {
        var assignments = labelAssignments.ToList();
        if (assignments.Count == 0) return 0;

        var entryIds = assignments.Select(a => a.entryId).Distinct().ToList();
        var labelIds = assignments.Select(a => a.labelId).Distinct().ToList();

        // Fetch all relevant entries and labels at once
        var entries = await context.StockEntries
            .Where(e => entryIds.Contains(e.EntryId))
            .Include(e => e.Labels)
            .ToListAsync(cancellationToken);

        var labels = await context.FinancialLabels
            .Where(l => labelIds.Contains(l.Id))
            .ToListAsync(cancellationToken);

        var entriesById = entries.ToDictionary(e => e.EntryId);
        var labelsById = labels.ToDictionary(l => l.Id);

        int addedCount = 0;

        foreach (var (entryId, labelId) in assignments)
        {
            if (!entriesById.TryGetValue(entryId, out var entry) || !labelsById.TryGetValue(labelId, out var label))
                continue;

            // Only add if not already present
            if (!entry.Labels.Any(l => l.Id == labelId))
            {
                entry.Labels.Add(label);
                addedCount++;
            }
        }

        if (addedCount > 0)
            await context.SaveChangesAsync(cancellationToken);

        return addedCount;
    }

    public async Task<IReadOnlyList<StockAccountEntry>> GetByIds(IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default)
    {
        if (entryIds.Count == 0)
            return [];

        return await context.StockEntries
            .Where(e => entryIds.Contains(e.EntryId))
            .ToListAsync(cancellationToken);
    }


}