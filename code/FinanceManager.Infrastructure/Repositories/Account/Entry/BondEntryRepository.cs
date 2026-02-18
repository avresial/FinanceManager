using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class BondEntryRepository(AppDbContext context) : IBondAccountEntryRepository<BondAccountEntry>
{
    public async Task<bool> Add(BondAccountEntry entry, bool recalculate)
    {
        // Don't use entry.Value as it may be a placeholder (-1)
        // The correct value will be calculated during recalculation
        var newEntry = new BondAccountEntry(entry.AccountId, 0, DateTime.SpecifyKind(entry.PostingDate, DateTimeKind.Utc),
         0, entry.ValueChange, entry.BondDetailsId)
        {
            Labels = entry.Labels,
        };

        context.BondEntries.Add(newEntry);
        await context.SaveChangesAsync();

        if (recalculate)
            await RecalculateValues(newEntry.AccountId, newEntry.EntryId);
        return true;
    }
    public async Task<bool> Add(IEnumerable<BondAccountEntry> entries, bool recalculate = true)
    {
        BondAccountEntry? firstEntry = null;

        foreach (var entry in entries)
        {
            // Don't use entry.Value as it may be a placeholder
            // The correct value will be calculated during recalculation
            var newEntry = new BondAccountEntry(entry.AccountId, 0, DateTime.SpecifyKind(entry.PostingDate, DateTimeKind.Utc),
             0, entry.ValueChange, entry.BondDetailsId)
            {
                Labels = entry.Labels,
            };

            if (firstEntry is null) firstEntry = newEntry;
            context.BondEntries.Add(newEntry);
        }

        await context.SaveChangesAsync();
        if (recalculate && firstEntry is not null)
            await RecalculateValues(firstEntry.AccountId, firstEntry.EntryId);

        return true;
    }

    public async Task<bool> Delete(int accountId, int entryId)
    {
        var entryToDelete = await context.BondEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entryToDelete is null) return false;
        context.BondEntries.Remove(entryToDelete);
        await context.SaveChangesAsync();
        await RecalculateValues(entryToDelete.AccountId, entryToDelete.PostingDate);

        return true;
    }

    public async Task<bool> Delete(int accountId)
    {
        var entriesToRemove = await context.BondEntries.Where(e => e.AccountId == accountId).ToListAsync();
        context.BondEntries.RemoveRange(entriesToRemove);
        await context.SaveChangesAsync();
        return true;
    }

    public IAsyncEnumerable<BondAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) => context.BondEntries
            .Where(x => x.AccountId == accountId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .Include(x => x.Labels)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .AsAsyncEnumerable();

    public async Task<BondAccountEntry?> Get(int accountId, int entryId) => await context.BondEntries
            .FirstOrDefaultAsync(x => x.AccountId == accountId && x.EntryId == entryId);

    public async Task<int> GetCount(int accountId) => await context.BondEntries.CountAsync(x => x.AccountId == accountId);

    public async Task<BondAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var existingEntry = await context.BondEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await context.BondEntries
            .Where(x => x.AccountId == accountId && x.PostingDate < existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();
    }

    public async Task<BondAccountEntry?> GetNextOlder(int accountId, DateTime date) => await context.BondEntries
             .Where(x => x.AccountId == accountId && x.PostingDate < date)
             .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
             .FirstOrDefaultAsync();

    public async Task<BondAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var existingEntry = await context.BondEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await context.BondEntries
            .Where(x => x.AccountId == accountId && x.PostingDate > existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();
    }

    public async Task<BondAccountEntry?> GetNextYounger(int accountId, DateTime date) => await context.BondEntries
            .Where(x => x.AccountId == accountId && x.PostingDate > date)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<BondAccountEntry?> GetOldest(int accountId) => await context.BondEntries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<BondAccountEntry?> GetYoungest(int accountId) => await context.BondEntries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();

    public async Task<bool> Update(BondAccountEntry entry)
    {
        var existingEntry = await context.BondEntries.Include(x => x.Labels).FirstOrDefaultAsync(e => e.AccountId == entry.AccountId && e.EntryId == entry.EntryId);
        if (existingEntry is null) return false;

        List<FinancialLabel> newLabels = [];
        foreach (var label in entry.Labels)
        {
            var existingLabel = await context.FinancialLabels.FirstOrDefaultAsync(x => x.Id == label.Id);
            if (existingLabel is null) continue;

            newLabels.Add(existingLabel);
        }

        entry.Labels = newLabels;

        existingEntry.Update(entry);
        await context.SaveChangesAsync();
        await RecalculateValues(entry.AccountId, entry.EntryId);
        return true;
    }

    public async Task RecalculateValues(int accountId, int entryId)
    {
        var entry = await context.BondEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return;

        // Get all entries from the posting date onwards, ordered by date and id
        var entriesToUpdate = await Get(accountId, entry.PostingDate, DateTime.UtcNow)
            .OrderBy(x => x.PostingDate)
            .ThenBy(x => x.EntryId)
            .ToListAsync();

        // Group by BondDetailsId to calculate values independently per bond
        var entriesByBond = entriesToUpdate.GroupBy(e => e.BondDetailsId);
        foreach (var bondGroup in entriesByBond)
        {
            // Get the previous entry for this specific bond
            var previousEntry = await context.BondEntries
                .Where(x => x.AccountId == accountId &&
                           x.BondDetailsId == bondGroup.Key &&
                           x.PostingDate < entry.PostingDate
                           && x.EntryId != entry.EntryId)
                .OrderByDescending(x => x.PostingDate)
                .ThenByDescending(x => x.EntryId)
                .FirstOrDefaultAsync();

            // Recalculate values for this bond's entries
            foreach (var entryToUpdate in bondGroup.OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId))
            {
                if (previousEntry is not null)
                    entryToUpdate.Value = previousEntry.Value + entryToUpdate.ValueChange;
                else
                    entryToUpdate.Value = entryToUpdate.ValueChange;

                previousEntry = entryToUpdate;
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task RecalculateValues(int accountId, DateTime startDate)
    {
        // Get all entries from the start date onwards, ordered by date and id
        var entriesToUpdate = await Get(accountId, startDate, DateTime.UtcNow)
            .OrderBy(x => x.PostingDate)
            .ThenBy(x => x.EntryId)
            .ToListAsync();

        // Group by BondDetailsId to calculate values independently per bond
        var entriesByBond = entriesToUpdate.GroupBy(e => e.BondDetailsId);

        foreach (var bondGroup in entriesByBond)
        {
            // Get the previous entry for this specific bond
            var previousEntry = await context.BondEntries
                .Where(x => x.AccountId == accountId &&
                           x.BondDetailsId == bondGroup.Key &&
                           x.PostingDate < startDate)
                .OrderByDescending(x => x.PostingDate)
                .ThenByDescending(x => x.EntryId)
                .FirstOrDefaultAsync();

            // Recalculate values for this bond's entries
            foreach (var entryToUpdate in bondGroup.OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId))
            {
                if (previousEntry is not null)
                    entryToUpdate.Value = previousEntry.Value + entryToUpdate.ValueChange;
                else
                    entryToUpdate.Value = entryToUpdate.ValueChange;

                previousEntry = entryToUpdate;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<bool> AddLabel(int entryId, int labelId)
    {
        var entry = await context.BondEntries.FirstOrDefaultAsync(e => e.EntryId == entryId);
        var label = await context.FinancialLabels.FirstOrDefaultAsync(l => l.Id == labelId);

        if (entry is null || label is null) return false;

        await context.SaveChangesAsync();

        return true;
    }

    public async Task<IReadOnlyList<BondAccountEntry>> GetByIds(IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default)
    {
        if (entryIds.Count == 0)
            return [];

        return await context.BondEntries
            .Where(e => entryIds.Contains(e.EntryId))
            .ToListAsync(cancellationToken);
    }

    async Task<Dictionary<int, BondAccountEntry>> IBondAccountEntryRepository<BondAccountEntry>.GetNextOlder(int accountId, DateTime date)
    {
        Dictionary<int, BondAccountEntry> result = [];

        var bondDetailsIds = await context.BondEntries
                                .Where(e => e.AccountId == accountId)
                                .Select(m => m.BondDetailsId)
                                .Distinct()
                                .ToListAsync();

        foreach (var bondDetailsId in bondDetailsIds)
        {
            var nextOlder = await context.BondEntries
                   .Where(e => e.BondDetailsId == bondDetailsId && e.AccountId == accountId && e.PostingDate < date)
                   .OrderByDescending(e => e.PostingDate).ThenByDescending(e => e.EntryId)
                   .FirstOrDefaultAsync();

            if (nextOlder is null) continue;

            result.Add(bondDetailsId, nextOlder);
        }

        return result;
    }

    async Task<Dictionary<int, BondAccountEntry>> IBondAccountEntryRepository<BondAccountEntry>.GetNextYounger(int accountId, DateTime date)
    {
        Dictionary<int, BondAccountEntry> result = [];

        var bondDetailsIds = await context.BondEntries
                                .Where(e => e.AccountId == accountId)
                                .Select(m => m.BondDetailsId)
                                .Distinct()
                                .ToListAsync();

        foreach (var bondDetailsId in bondDetailsIds)
        {
            var nextYounger = await context.BondEntries
                   .Where(e => e.BondDetailsId == bondDetailsId && e.AccountId == accountId && e.PostingDate > date)
                   .OrderByDescending(e => e.PostingDate).ThenByDescending(e => e.EntryId)
                   .LastOrDefaultAsync();

            if (nextYounger is null) continue;

            result.Add(bondDetailsId, nextYounger);
        }

        return result;
    }


}