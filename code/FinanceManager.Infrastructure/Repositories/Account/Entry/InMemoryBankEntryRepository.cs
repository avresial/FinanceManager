using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class InMemoryBankEntryRepository : IAccountEntryRepository<BankAccountEntry>
{
    private readonly AppDbContext _dbContext;

    public InMemoryBankEntryRepository(AppDbContext context)
    {
        _dbContext = context;
    }

    public async Task<bool> Add(BankAccountEntry entry)
    {
        BankAccountEntry newBankAccountEntry = new(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            Labels = entry.Labels,
        };

        _dbContext.BankEntries.Add(newBankAccountEntry);
        await _dbContext.SaveChangesAsync();
        await RecalculateValues(newBankAccountEntry.AccountId, newBankAccountEntry.EntryId);
        return true;
    }

    public async Task<bool> Delete(int accountId, int entryId)
    {
        var entryToDelete = await _dbContext.BankEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entryToDelete == null) return false;
        _dbContext.BankEntries.Remove(entryToDelete);
        await _dbContext.SaveChangesAsync();
        await RecalculateValues(entryToDelete.AccountId, entryToDelete.PostingDate);
        return true;
    }

    public async Task<bool> Delete(int accountId)
    {
        var entriesToRemove = await _dbContext.BankEntries.Where(e => e.AccountId == accountId).ToListAsync();
        _dbContext.BankEntries.RemoveRange(entriesToRemove);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<BankAccountEntry>> Get(int accountId, DateTime startDate, DateTime endDate) => await _dbContext.BankEntries
            .Where(x => x.AccountId == accountId && x.PostingDate >= startDate && x.PostingDate <= endDate).Include(x => x.Labels)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId).ToListAsync();
    public async Task<BankAccountEntry?> Get(int accountId, int entryId) => await _dbContext.BankEntries
            .FirstOrDefaultAsync(x => x.AccountId == accountId && x.EntryId == entryId);
    public async Task<int?> GetCount(int accountId) => await _dbContext.BankEntries.CountAsync(x => x.AccountId == accountId);

    public async Task<BankAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var existingEntry = await _dbContext.BankEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await _dbContext.BankEntries
            .Where(x => x.AccountId == accountId && x.PostingDate < existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();
    }

    public async Task<BankAccountEntry?> GetNextOlder(int accountId, DateTime date) => await _dbContext.BankEntries
             .Where(x => x.AccountId == accountId && x.PostingDate < date)
             .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
             .FirstOrDefaultAsync();

    public async Task<BankAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var existingEntry = await _dbContext.BankEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await _dbContext.BankEntries
            .Where(x => x.AccountId == accountId && x.PostingDate > existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();
    }

    public async Task<BankAccountEntry?> GetNextYounger(int accountId, DateTime date) => await _dbContext.BankEntries
            .Where(x => x.AccountId == accountId && x.PostingDate > date)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<BankAccountEntry?> GetOldest(int accountId) => await _dbContext.BankEntries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<BankAccountEntry?> GetYoungest(int accountId) => await _dbContext.BankEntries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();

    public async Task<bool> Update(BankAccountEntry entry)
    {
        var existingEntry = await _dbContext.BankEntries.Include(x => x.Labels).FirstOrDefaultAsync(e => e.AccountId == entry.AccountId && e.EntryId == entry.EntryId);
        if (existingEntry is null) return false;

        List<FinancialLabel> newLabels = [];
        foreach (var label in entry.Labels)
        {
            var existingLabel = await _dbContext.FinancialLabels.FirstOrDefaultAsync(x => x.Id == label.Id);
            if (existingLabel is null) continue;

            newLabels.Add(existingLabel);
        }

        entry.Labels = newLabels;

        existingEntry.Update(entry);
        await _dbContext.SaveChangesAsync();
        await RecalculateValues(entry.AccountId, entry.EntryId);
        return true;
    }

    private async Task RecalculateValues(int accountId, int entryId)
    {
        var entry = await _dbContext.BankEntries.FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return;

        var entriesToUpdate = await Get(accountId, entry.PostingDate, DateTime.UtcNow);
        var previousEntry = await GetNextOlder(accountId, entry.PostingDate);

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
        await _dbContext.SaveChangesAsync();
    }

    private int GetHighestEntry() => _dbContext.BankEntries
            .ToList()
            .Select(x => x.EntryId)
            .DefaultIfEmpty(0)
            .Max();

    public async Task<bool> AddLabel(int entryId, int labelId)
    {
        var entry = await _dbContext.BankEntries.FirstOrDefaultAsync(e => e.EntryId == entryId);
        var label = await _dbContext.FinancialLabels.FirstOrDefaultAsync(l => l.Id == labelId);

        if (entry is null || label is null) return false;

        //if (entry.LabelBankEntries.Any(l => l.FinancialLabelId == labelId)) return false;

        //_dbContext.LabelBankEntries.Add(new FinancialLabelBankAccountEntry
        //{
        //    FinancialLabelId = labelId,
        //    FinancialLabel = label,
        //    BankAccountEntryId = entryId,
        //    BankAccountEntry = entry
        //});

        await _dbContext.SaveChangesAsync();

        return true;

    }
}
