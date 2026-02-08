using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class CurrencyEntryRepository(AppDbContext context) : IAccountEntryRepository<CurrencyAccountEntry>
{
    public async Task<bool> Add(CurrencyAccountEntry entry, bool recalculate)
    {
        CurrencyAccountEntry newAccountEntry = new(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            ContractorDetails = entry.ContractorDetails,
            Labels = entry.Labels,
        };

        context.CurrencyEntries.Add(newAccountEntry);
        await context.SaveChangesAsync();
        if (recalculate)
            await RecalculateValues(newAccountEntry.AccountId, newAccountEntry.EntryId);
        return true;
    }
    public async Task<bool> Add(IEnumerable<CurrencyAccountEntry> entries, bool recalculate = true)
    {
        CurrencyAccountEntry? firstEntry = null;

        foreach (var entry in entries)
        {
            CurrencyAccountEntry newEntry = new(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange)
            {
                Description = entry.Description,
                ContractorDetails = entry.ContractorDetails,
                Labels = entry.Labels,
            };

            if (firstEntry is null) firstEntry = newEntry;

            context.CurrencyEntries.Add(newEntry);
        }

        await context.SaveChangesAsync();
        if (recalculate && firstEntry is not null)
            await RecalculateValues(firstEntry.AccountId, firstEntry.EntryId);
        return true;
    }

    public async Task<bool> Delete(int accountId, int entryId)
    {
        var entryToDelete = await context.CurrencyEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entryToDelete is null) return false;
        context.CurrencyEntries.Remove(entryToDelete);
        await context.SaveChangesAsync();
        await RecalculateValues(entryToDelete.AccountId, entryToDelete.PostingDate);

        return true;
    }

    public async Task<bool> Delete(int accountId)
    {
        var entriesToRemove = await context.CurrencyEntries.Where(e => e.AccountId == accountId).ToListAsync();
        context.CurrencyEntries.RemoveRange(entriesToRemove);
        await context.SaveChangesAsync();
        return true;
    }

    public IAsyncEnumerable<CurrencyAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) => context.CurrencyEntries
            .Where(x => x.AccountId == accountId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .Include(x => x.Labels)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .AsAsyncEnumerable();

    public async Task<CurrencyAccountEntry?> Get(int accountId, int entryId) => await context.CurrencyEntries
            .FirstOrDefaultAsync(x => x.AccountId == accountId && x.EntryId == entryId);

    public async Task<int> GetCount(int accountId) => await context.CurrencyEntries.CountAsync(x => x.AccountId == accountId);

    public async Task<CurrencyAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var existingEntry = await context.CurrencyEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await context.CurrencyEntries
            .Where(x => x.AccountId == accountId && x.PostingDate < existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();
    }

    public async Task<CurrencyAccountEntry?> GetNextOlder(int accountId, DateTime date) => await context.CurrencyEntries
             .Where(x => x.AccountId == accountId && x.PostingDate < date)
             .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
             .FirstOrDefaultAsync();

    public async Task<CurrencyAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var existingEntry = await context.CurrencyEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await context.CurrencyEntries
            .Where(x => x.AccountId == accountId && x.PostingDate > existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();
    }

    public async Task<CurrencyAccountEntry?> GetNextYounger(int accountId, DateTime date) => await context.CurrencyEntries
            .Where(x => x.AccountId == accountId && x.PostingDate > date)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<CurrencyAccountEntry?> GetOldest(int accountId) => await context.CurrencyEntries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<CurrencyAccountEntry?> GetYoungest(int accountId) => await context.CurrencyEntries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();

    public async Task<bool> Update(CurrencyAccountEntry entry)
    {
        var existingEntry = await context.CurrencyEntries.Include(x => x.Labels).FirstOrDefaultAsync(e => e.AccountId == entry.AccountId && e.EntryId == entry.EntryId);
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
        var entry = await context.CurrencyEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return;

        var previousEntry = await GetNextOlder(accountId, entry.PostingDate);

        await foreach (var entryToUpdate in Get(accountId, entry.PostingDate, DateTime.UtcNow).OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId))
        {
            if (previousEntry is not null)
                entryToUpdate.Value = previousEntry.Value + entryToUpdate.ValueChange;
            else
                entryToUpdate.Value = entryToUpdate.ValueChange;

            previousEntry = entryToUpdate;
        }

        context.SaveChanges();
    }

    private async Task RecalculateValues(int accountId, DateTime startDate)
    {
        var previousEntry = await GetNextOlder(accountId, startDate);

        await foreach (var entryToUpdate in Get(accountId, startDate, DateTime.UtcNow).OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId))
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
        var entry = await context.CurrencyEntries.FirstOrDefaultAsync(e => e.EntryId == entryId);
        var label = await context.FinancialLabels.FirstOrDefaultAsync(l => l.Id == labelId);

        if (entry is null || label is null) return false;

        await context.SaveChangesAsync();

        return true;
    }


}