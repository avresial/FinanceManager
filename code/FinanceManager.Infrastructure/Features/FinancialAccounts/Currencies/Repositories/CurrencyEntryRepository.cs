using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;

public class CurrencyEntryRepository(AppDbContext context) : IAccountEntryRepository<CurrencyAccountEntry>
{
    private readonly CurrencyEntryValueCalculator _valueCalculator = new(context);
    public Task<bool> Add(CurrencyAccountEntry entry, bool recalculate) =>
        Add(entry, recalculate, CancellationToken.None);

    public async Task<bool> Add(CurrencyAccountEntry entry, bool recalculate, CancellationToken cancellationToken)
    {
        CurrencyAccountEntry newAccountEntry = new(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            ContractorDetails = entry.ContractorDetails,
            Labels = await ResolveTrackedLabels(entry.Labels, cancellationToken),
        };

        if (context.Database.IsRelational() && recalculate)
        {
            // Relational: transactional mutation + recalculation commit together atomically.
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.CurrencyEntries.Add(newAccountEntry);
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues(newAccountEntry.AccountId, newAccountEntry.EntryId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            // Non-relational or no recalculation: persist entry first.
            context.CurrencyEntries.Add(newAccountEntry);
            await context.SaveChangesAsync(cancellationToken);
            if (recalculate)
            {
                // Post-commit repair: complete recalculation before propagating cancellation.
                try
                {
                    await RecalculateValues(newAccountEntry.AccountId, newAccountEntry.EntryId, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Provider timeout or internal cancellation: complete recalculation with no-token fallback.
                    await RecalculateValues(newAccountEntry.AccountId, newAccountEntry.EntryId, CancellationToken.None);
                }
            }
        }
        return true;
    }
    public Task<bool> Add(IEnumerable<CurrencyAccountEntry> entries, bool recalculate = true) =>
        Add(entries, recalculate, CancellationToken.None);

    public async Task<bool> Add(
        IEnumerable<CurrencyAccountEntry> entries,
        bool recalculate,
        CancellationToken cancellationToken)
    {
        var entryList = entries as IList<CurrencyAccountEntry> ?? entries.ToList();

        // Re-resolve already-persisted labels to context-tracked instances in one query so EF reuses the
        // existing rows instead of trying to INSERT detached copies — the guest seeder reads labels via
        // AsNoTracking and attaches them to these new entries. #408
        var existingLabelIds = entryList.SelectMany(e => e.Labels).Where(l => l.Id != 0).Select(l => l.Id).Distinct().ToList();
        var trackedById = existingLabelIds.Count == 0
            ? []
            : await context.FinancialLabels.Where(l => existingLabelIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        CurrencyAccountEntry? firstEntry = null;

        foreach (var entry in entryList)
        {
            CurrencyAccountEntry newEntry = new(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange)
            {
                Description = entry.Description,
                ContractorDetails = entry.ContractorDetails,
                Labels = entry.Labels.Select(l => l.Id != 0 && trackedById.TryGetValue(l.Id, out var tracked) ? tracked : l).ToList(),
            };

            if (firstEntry is null) firstEntry = newEntry;

            context.CurrencyEntries.Add(newEntry);
        }

        if (context.Database.IsRelational() && recalculate && firstEntry is not null)
        {
            // Relational: transactional mutation + recalculation commit together atomically.
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues(firstEntry.AccountId, firstEntry.EntryId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            // Non-relational or no recalculation: persist entries first.
            await context.SaveChangesAsync(cancellationToken);
            if (recalculate && firstEntry is not null)
            {
                // Post-commit repair: complete recalculation before propagating cancellation.
                try
                {
                    await RecalculateValues(firstEntry.AccountId, firstEntry.EntryId, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Provider timeout or internal cancellation: complete recalculation with no-token fallback.
                    await RecalculateValues(firstEntry.AccountId, firstEntry.EntryId, CancellationToken.None);
                }
            }
        }
        return true;
    }

    // Maps existing labels (Id != 0) to their context-tracked instances so EF does not re-insert detached
    // copies; brand-new labels (Id == 0) are passed through unchanged to be inserted. #408
    private async Task<List<FinancialLabel>> ResolveTrackedLabels(
        ICollection<FinancialLabel> labels,
        CancellationToken cancellationToken)
    {
        if (labels.Count == 0) return [];

        var existingIds = labels.Where(l => l.Id != 0).Select(l => l.Id).Distinct().ToList();
        var trackedById = existingIds.Count == 0
            ? []
            : await context.FinancialLabels.Where(l => existingIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        return labels
            .Select(l => l.Id != 0 && trackedById.TryGetValue(l.Id, out var tracked) ? tracked : l)
            .ToList();
    }

    public Task<bool> Delete(int accountId, int entryId) =>
        Delete(accountId, entryId, CancellationToken.None);

    public async Task<bool> Delete(int accountId, int entryId, CancellationToken cancellationToken)
    {
        var entryToDelete = await context.CurrencyEntries.FirstOrDefaultAsync(
            e => e.AccountId == accountId && e.EntryId == entryId,
            cancellationToken);
        if (entryToDelete is null) return false;

        var deletedAccountId = entryToDelete.AccountId;
        var deletedPostingDate = entryToDelete.PostingDate;

        if (context.Database.IsRelational())
        {
            // Relational: transactional mutation + recalculation commit together atomically.
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.CurrencyEntries.Remove(entryToDelete);
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues(deletedAccountId, deletedPostingDate, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            // Non-relational: persist deletion first, then repair with post-commit recalculation.
            context.CurrencyEntries.Remove(entryToDelete);
            await context.SaveChangesAsync(cancellationToken);
            // Post-commit repair: complete recalculation before propagating cancellation.
            try
            {
                await RecalculateValues(deletedAccountId, deletedPostingDate, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Provider timeout or internal cancellation: complete recalculation with no-token fallback.
                await RecalculateValues(deletedAccountId, deletedPostingDate, CancellationToken.None);
            }
        }

        return true;
    }

    public Task<bool> Delete(int accountId) => Delete(accountId, CancellationToken.None);

    public async Task<bool> Delete(int accountId, CancellationToken cancellationToken)
    {
        if (context.Database.IsRelational())
        {
            var deleted = await context.CurrencyEntries
                .Where(e => e.AccountId == accountId)
                .ExecuteDeleteAsync(cancellationToken);
            return deleted > 0;
        }

        var entriesToRemove = await context.CurrencyEntries
            .Where(e => e.AccountId == accountId)
            .ToListAsync(cancellationToken);
        context.CurrencyEntries.RemoveRange(entriesToRemove);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public IAsyncEnumerable<CurrencyAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) =>
        Get(accountId, startDate, endDate, CancellationToken.None);

    public async IAsyncEnumerable<CurrencyAccountEntry> Get(
        int accountId,
        DateTime startDate,
        DateTime endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var entry in context.CurrencyEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .Include(x => x.Labels)
            .ThenInclude(l => l.Classifications)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            yield return entry;
        }
    }

    public async Task<List<DateTime>> GetPostingDates(int accountId) => await context.CurrencyEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .Select(x => x.PostingDate)
            .OrderByDescending(date => date)
            .ToListAsync();

    public async Task<(List<CurrencyAccountEntry> Entries, DateTime EffectiveStartDate)> GetEntriesWithMinimumCount(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumEntryCount);

        if (minimumEntryCount == 0)
            return (await Get(accountId, startDate, endDate).ToListAsync(), startDate);

        var candidateDates = (await GetPostingDates(accountId))
            .Where(date => date <= endDate)
            .ToList();

        if (candidateDates.Count <= minimumEntryCount)
        {
            var oldestDate = candidateDates.Count != 0 && candidateDates[^1] < startDate ? candidateDates[^1] : startDate;
            return (await Get(accountId, oldestDate, endDate).ToListAsync(), oldestDate);
        }

        var nthNewestDate = candidateDates[minimumEntryCount - 1];
        var effectiveStartDate = nthNewestDate < startDate ? nthNewestDate : startDate;
        var entries = await Get(accountId, effectiveStartDate, endDate).ToListAsync();
        return (entries, effectiveStartDate);
    }

    public async Task<List<CurrencyAccountEntry>> Get(int accountId, DateTime date, int count, bool olderThenDate = true)
    {
        if (count <= 0) return [];

        if (olderThenDate)
        {
            return await context.CurrencyEntries
                .AsNoTracking()
                .Where(e => e.AccountId == accountId && e.PostingDate <= date)
                .Include(e => e.Labels)
                .ThenInclude(l => l.Classifications)
                .AsSplitQuery()
                .OrderByDescending(e => e.PostingDate)
                .ThenByDescending(e => e.EntryId)
                .Take(count)
                .ToListAsync();
        }

        return await context.CurrencyEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate >= date)
            .Include(e => e.Labels)
            .ThenInclude(l => l.Classifications)
            .AsSplitQuery()
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.EntryId)
            .Take(count)
            .ToListAsync();
    }

    public async Task<CurrencyAccountEntry?> Get(int accountId, int entryId) => await context.CurrencyEntries
            .AsNoTracking()
            .Include(entry => entry.Labels)
            .FirstOrDefaultAsync(x => x.AccountId == accountId && x.EntryId == entryId);

    public async Task<int> GetCount(int accountId) => await context.CurrencyEntries.AsNoTracking().CountAsync(x => x.AccountId == accountId);

    public async Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<int, int>();

        return await (
            from entry in context.CurrencyEntries.AsNoTracking()
            join account in context.Accounts on entry.AccountId equals account.AccountId
            where userIds.Contains(account.UserId)
            group entry by account.UserId into grouped
            select new { UserId = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);
    }

    public async Task<CurrencyAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var existingEntry = await context.CurrencyEntries.AsNoTracking().FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await context.CurrencyEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate < existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();
    }

    public async Task<CurrencyAccountEntry?> GetNextOlder(int accountId, DateTime date) => await context.CurrencyEntries
             .AsNoTracking()
             .Where(x => x.AccountId == accountId && x.PostingDate < date)
             .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
             .FirstOrDefaultAsync();

    public async Task<CurrencyAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var existingEntry = await context.CurrencyEntries.AsNoTracking().FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await context.CurrencyEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate > existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();
    }

    public async Task<CurrencyAccountEntry?> GetNextYounger(int accountId, DateTime date) => await context.CurrencyEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate > date)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<List<CurrencyAccountEntry>> GetRange(IReadOnlyCollection<int> accountIds, DateTime startDate, DateTime endDate)
    {
        if (accountIds.Count == 0) return [];

        return await context.CurrencyEntries
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .Include(x => x.Labels)
            .ThenInclude(l => l.Classifications)
            .AsSplitQuery()
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .ToListAsync();
    }

    public async Task<List<CurrencyAccountEntry>> GetValueRange(IReadOnlyCollection<int> accountIds, DateTime startDate, DateTime endDate)
    {
        if (accountIds.Count == 0) return [];

        return await context.CurrencyEntries
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .ToListAsync();
    }

    public async Task<Dictionary<int, CurrencyAccountEntry>> GetNextOlder(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        // One row per account: the entry that no other older-than-date entry of the same account beats on
        // (PostingDate, EntryId). EntryId is unique, so exactly one row per account survives the filter.
        var rows = await context.CurrencyEntries
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate < date)
            .Where(e => !context.CurrencyEntries.Any(o => o.AccountId == e.AccountId && o.PostingDate < date
                && (o.PostingDate > e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId > e.EntryId))))
            .ToListAsync();

        return rows.ToDictionary(e => e.AccountId);
    }

    public async Task<Dictionary<int, CurrencyAccountEntry>> GetNextYounger(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.CurrencyEntries
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate > date)
            .Where(e => !context.CurrencyEntries.Any(o => o.AccountId == e.AccountId && o.PostingDate > date
                && (o.PostingDate < e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId < e.EntryId))))
            .ToListAsync();

        return rows.ToDictionary(e => e.AccountId);
    }

    public async Task<CurrencyAccountEntry?> GetOldest(int accountId) => await context.CurrencyEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<CurrencyAccountEntry?> GetYoungest(int accountId) => await context.CurrencyEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();

    public async Task<bool> Update(CurrencyAccountEntry entry)
    {
        var existingEntry = await context.CurrencyEntries.Include(x => x.Labels).FirstOrDefaultAsync(e => e.AccountId == entry.AccountId && e.EntryId == entry.EntryId);
        if (existingEntry is null) return false;

        var labelIds = entry.Labels.Select(x => x.Id).ToList();
        entry.Labels = await context.FinancialLabels
            .Where(x => labelIds.Contains(x.Id))
            .ToListAsync();

        existingEntry.Update(entry);
        await context.SaveChangesAsync();
        await RecalculateValues(entry.AccountId, entry.EntryId);
        return true;
    }

    public async Task<bool> AddLabel(int entryId, int labelId)
    {
        var entry = await context.CurrencyEntries
            .Include(e => e.Labels)
            .FirstOrDefaultAsync(e => e.EntryId == entryId);
        var label = await context.FinancialLabels.FirstOrDefaultAsync(l => l.Id == labelId);

        if (entry is null || label is null) return false;

        // Check if label already exists before adding
        if (entry.Labels.Any(l => l.Id == labelId)) return true;

        entry.Labels.Add(label);
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
        var entries = await context.CurrencyEntries
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
    public Task RecalculateValues(int accountId, int entryId) =>
        RecalculateValues(accountId, entryId, CancellationToken.None);

    public async Task RecalculateValues(int accountId, int entryId, CancellationToken cancellationToken)
    {
        var startDate = await context.CurrencyEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.EntryId == entryId)
            .Select(e => (DateTime?)e.PostingDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (startDate is not DateTime date) return;

        await RecalculateValues(accountId, date, cancellationToken);
    }

    public Task RecalculateValues(int accountId) => RecalculateValues(accountId, CancellationToken.None);

    public async Task RecalculateValues(int accountId, CancellationToken cancellationToken)
    {
        // Recalculate from the oldest entry: the anchor before it is empty, so the whole account is
        // rebuilt from a zero running balance.
        var startDate = await context.CurrencyEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.EntryId)
            .Select(e => (DateTime?)e.PostingDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (startDate is not DateTime date) return;

        await RecalculateValues(accountId, date, cancellationToken);
    }

    private async Task RecalculateValues(int accountId, DateTime startDate, CancellationToken cancellationToken)
        => await _valueCalculator.Recalculate(accountId, startDate, cancellationToken);

    public async Task<IReadOnlyList<CurrencyAccountEntry>> GetByIds(IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default)
    {
        if (entryIds.Count == 0)
            return [];

        return await context.CurrencyEntries
            .AsNoTracking()
            .Where(e => entryIds.Contains(e.EntryId))
            .Include(e => e.Labels)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CurrencyAccountEntry>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        return await context.CurrencyEntries
            .AsNoTracking()
            .Where(e => !e.Labels.Any())
            .Where(e => e.Description != null && e.Description != "")
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.EntryId)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}