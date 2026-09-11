using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;

public class BondEntryRepository(AppDbContext context) : IBondAccountEntryRepository<BondAccountEntry>
{
    // SQL Server permits at most 2,100 parameters per command. Keep room for the Take parameter and
    // future predicates when a user has an unusually large number of accounts.
    private const int _maxAccountIdsPerQuery = 2_000;

    private readonly BondEntryValueCalculator _valueCalculator = new(context);
    private static readonly System.Reflection.PropertyInfo? _entryIdProperty =
        typeof(FinancialEntryBase).GetProperty(nameof(FinancialEntryBase.EntryId));

    public Task<bool> Add(BondAccountEntry entry, bool recalculate) =>
        Add(entry, recalculate, CancellationToken.None);

    public async Task<bool> Add(BondAccountEntry entry, bool recalculate, CancellationToken cancellationToken)
    {
        // Don't use entry.Value as it may be a placeholder (-1)
        // The correct value will be calculated during recalculation
        var newEntry = new BondAccountEntry(entry.AccountId, 0, DateTime.SpecifyKind(entry.PostingDate, DateTimeKind.Utc),
         0, entry.ValueChange, entry.BondDetailsId)
        {
            Labels = await ResolveTrackedLabels(entry.Labels, cancellationToken),
        };

        if (context.Database.IsRelational() && recalculate)
        {
            // Relational: transactional mutation + recalculation commit together atomically.
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.BondEntries.Add(newEntry);
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues(newEntry.AccountId, newEntry.EntryId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (entry.EntryId == 0 && newEntry.EntryId != 0)
                _entryIdProperty?.SetValue(entry, newEntry.EntryId);
            context.ChangeTracker.Clear();
        }
        else
        {
            // Non-relational or no recalculation: persist entry first.
            context.BondEntries.Add(newEntry);
            await context.SaveChangesAsync(cancellationToken);
            if (entry.EntryId == 0 && newEntry.EntryId != 0)
                _entryIdProperty?.SetValue(entry, newEntry.EntryId);
            if (recalculate)
            {
                // Post-commit repair: complete recalculation before propagating cancellation.
                try
                {
                    await RecalculateValues(newEntry.AccountId, newEntry.EntryId, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Provider timeout or internal cancellation: complete recalculation with no-token fallback.
                    await RecalculateValues(newEntry.AccountId, newEntry.EntryId, CancellationToken.None);
                }
            }
            context.ChangeTracker.Clear();
        }
        return true;
    }
    public Task<bool> Add(IEnumerable<BondAccountEntry> entries, bool recalculate = true) =>
        Add(entries, recalculate, CancellationToken.None);

    public async Task<bool> Add(
        IEnumerable<BondAccountEntry> entries,
        bool recalculate,
        CancellationToken cancellationToken)
    {
        var entryList = entries as IList<BondAccountEntry> ?? entries.ToList();
        if (entryList.Count == 0) return true;

        var existingLabelIds = entryList.SelectMany(e => e.Labels).Where(l => l.Id != 0).Select(l => l.Id).Distinct().ToList();
        var trackedById = existingLabelIds.Count == 0
            ? []
            : await context.FinancialLabels.Where(l => existingLabelIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        List<(BondAccountEntry Source, BondAccountEntry Target)> pairs = new(entryList.Count);
        BondAccountEntry? oldestEntry = null;

        foreach (var entry in entryList)
        {
            // Don't use entry.Value as it may be a placeholder
            // The correct value will be calculated during recalculation
            var newEntry = new BondAccountEntry(entry.AccountId, 0, DateTime.SpecifyKind(entry.PostingDate, DateTimeKind.Utc),
             0, entry.ValueChange, entry.BondDetailsId)
            {
                Labels = entry.Labels.Select(l => l.Id != 0 && trackedById.TryGetValue(l.Id, out var tracked) ? tracked : l).ToList(),
            };

            pairs.Add((entry, newEntry));
            if (oldestEntry is null || newEntry.PostingDate < oldestEntry.PostingDate)
                oldestEntry = newEntry;

            context.BondEntries.Add(newEntry);
        }

        if (context.Database.IsRelational() && recalculate && oldestEntry is not null)
        {
            // Relational: transactional mutation + recalculation commit together atomically.
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues(oldestEntry.AccountId, oldestEntry.EntryId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            foreach (var (source, target) in pairs)
            {
                if (source.EntryId == 0 && target.EntryId != 0)
                    _entryIdProperty?.SetValue(source, target.EntryId);
            }
            context.ChangeTracker.Clear();
        }
        else
        {
            // Non-relational or no recalculation: persist entries first.
            await context.SaveChangesAsync(cancellationToken);
            foreach (var (source, target) in pairs)
            {
                if (source.EntryId == 0 && target.EntryId != 0)
                    _entryIdProperty?.SetValue(source, target.EntryId);
            }
            if (recalculate && oldestEntry is not null)
            {
                // Post-commit repair: complete recalculation before propagating cancellation.
                try
                {
                    await RecalculateValues(oldestEntry.AccountId, oldestEntry.EntryId, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Provider timeout or internal cancellation: complete recalculation with no-token fallback.
                    await RecalculateValues(oldestEntry.AccountId, oldestEntry.EntryId, CancellationToken.None);
                }
            }
            context.ChangeTracker.Clear();
        }

        return true;
    }

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
        var entryToDelete = await context.BondEntries
            .FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId, cancellationToken);
        if (entryToDelete is null) return false;
        context.BondEntries.Remove(entryToDelete);
        await context.SaveChangesAsync(cancellationToken);
        await RecalculateValues(entryToDelete.AccountId, entryToDelete.PostingDate, cancellationToken);

        return true;
    }

    public Task<bool> Delete(int accountId) => Delete(accountId, CancellationToken.None);

    public async Task<bool> Delete(int accountId, CancellationToken cancellationToken)
    {
        if (context.Database.IsRelational())
        {
            var deleted = await context.BondEntries
                .Where(e => e.AccountId == accountId)
                .ExecuteDeleteAsync(cancellationToken);
            return deleted > 0;
        }

        var entriesToRemove = await context.BondEntries.Where(e => e.AccountId == accountId).ToListAsync(cancellationToken);
        context.BondEntries.RemoveRange(entriesToRemove);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public IAsyncEnumerable<BondAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) =>
        Get(accountId, startDate, endDate, CancellationToken.None);

    public async IAsyncEnumerable<BondAccountEntry> Get(
        int accountId,
        DateTime startDate,
        DateTime endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var entry in context.BondEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .Include(x => x.Labels)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            yield return entry;
        }
    }

    public async Task<List<DateTime>> GetPostingDates(int accountId) => await context.BondEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .Select(x => x.PostingDate)
            .ToListAsync();

    public async Task<(List<BondAccountEntry> Entries, DateTime EffectiveStartDate)> GetEntriesWithMinimumCount(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumEntryCount);

        var rangeEntries = await Get(accountId, startDate, endDate).ToListAsync();

        if (minimumEntryCount == 0 || rangeEntries.Count >= minimumEntryCount)
            return (rangeEntries, startDate);

        var candidateDates = (await GetPostingDates(accountId))
            .Where(date => date <= endDate)
            .OrderByDescending(date => date)
            .ToList();

        if (candidateDates.Count <= minimumEntryCount)
        {
            var oldestDate = candidateDates.Count != 0 && candidateDates[^1] < startDate ? candidateDates[^1] : startDate;
            var expandedEntries = oldestDate == startDate ? rangeEntries : await Get(accountId, oldestDate, endDate).ToListAsync();
            return (expandedEntries, oldestDate);
        }

        var nthNewestDate = candidateDates[minimumEntryCount - 1];
        var effectiveStartDate = nthNewestDate < startDate ? nthNewestDate : startDate;
        var entries = await Get(accountId, effectiveStartDate, endDate).ToListAsync();
        return (entries, effectiveStartDate);
    }

    public async Task<List<BondAccountEntry>> Get(int accountId, DateTime date, int count, bool olderThenDate = true)
    {
        if (count <= 0) return [];

        if (olderThenDate)
        {
            return await context.BondEntries
                .AsNoTracking()
                .Where(e => e.AccountId == accountId && e.PostingDate <= date)
                .Include(e => e.Labels)
                .AsSplitQuery()
                .OrderByDescending(e => e.PostingDate)
                .ThenByDescending(e => e.EntryId)
                .Take(count)
                .ToListAsync();
        }

        var entries = await context.BondEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate >= date)
            .Include(e => e.Labels)
            .AsSplitQuery()
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.EntryId)
            .Take(count)
            .ToListAsync();

        return entries
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.EntryId)
            .ToList();
    }

    public async Task<BondAccountEntry?> Get(int accountId, int entryId) => await context.BondEntries
            .AsNoTracking()
            .Include(entry => entry.Labels)
            .FirstOrDefaultAsync(x => x.AccountId == accountId && x.EntryId == entryId);

    public async Task<int> GetCount(int accountId) => await context.BondEntries.AsNoTracking().CountAsync(x => x.AccountId == accountId);

    public async Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<int, int>();

        return await (
            from entry in context.BondEntries.AsNoTracking()
            join account in context.Accounts on entry.AccountId equals account.AccountId
            where userIds.Contains(account.UserId)
            group entry by account.UserId into grouped
            select new { UserId = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);
    }

    public async Task<BondAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var existingEntry = await context.BondEntries.AsNoTracking().FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await context.BondEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate < existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .FirstOrDefaultAsync();
    }

    public async Task<BondAccountEntry?> GetNextOlder(int accountId, DateTime date) => await context.BondEntries
             .AsNoTracking()
             .Where(x => x.AccountId == accountId && x.PostingDate < date)
             .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
             .FirstOrDefaultAsync();

    public async Task<BondAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var existingEntry = await context.BondEntries.AsNoTracking().FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (existingEntry is null) return default;

        return await context.BondEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate > existingEntry.PostingDate)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();
    }

    public async Task<BondAccountEntry?> GetNextYounger(int accountId, DateTime date) => await context.BondEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate > date)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<List<BondAccountEntry>> GetRange(IReadOnlyCollection<int> accountIds, DateTime startDate, DateTime endDate)
    {
        if (accountIds.Count == 0) return [];

        return await context.BondEntries
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .Include(x => x.Labels)
            .AsSplitQuery()
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .ToListAsync();
    }

    public async Task<List<BondAccountEntry>> GetValueRange(IReadOnlyCollection<int> accountIds, DateTime startDate, DateTime endDate)
    {
        if (accountIds.Count == 0) return [];

        return await context.BondEntries
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .ToListAsync();
    }

    public async Task<Dictionary<int, BondAccountEntry>> GetNextOlder(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.BondEntries
            .AsNoTracking()
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate < date)
            .Where(e => !context.BondEntries.Any(o => o.AccountId == e.AccountId && o.PostingDate < date
                && (o.PostingDate > e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId > e.EntryId))))
            .ToListAsync();

        return rows.ToDictionary(e => e.AccountId);
    }

    public async Task<Dictionary<int, BondAccountEntry>> GetNextYounger(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.BondEntries
            .AsNoTracking()
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate > date)
            .Where(e => !context.BondEntries.Any(o => o.AccountId == e.AccountId && o.PostingDate > date
                && (o.PostingDate < e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId < e.EntryId))))
            .ToListAsync();

        return rows.ToDictionary(e => e.AccountId);
    }

    public async Task<Dictionary<int, Dictionary<int, BondAccountEntry>>> GetNextOlderPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        // One row per (account, bond details id): the entry no other older-than-date entry of the same
        // account+bond beats on (PostingDate, EntryId).
        var rows = await context.BondEntries
            .AsNoTracking()
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate < date)
            .Where(e => !context.BondEntries.Any(o => o.AccountId == e.AccountId && o.BondDetailsId == e.BondDetailsId && o.PostingDate < date
                && (o.PostingDate > e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId > e.EntryId))))
            .ToListAsync();

        return rows.GroupBy(e => e.AccountId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.BondDetailsId));
    }

    public async Task<Dictionary<int, Dictionary<int, BondAccountEntry>>> GetNextYoungerPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.BondEntries
            .AsNoTracking()
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate > date)
            .Where(e => !context.BondEntries.Any(o => o.AccountId == e.AccountId && o.BondDetailsId == e.BondDetailsId && o.PostingDate > date
                && (o.PostingDate < e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId < e.EntryId))))
            .ToListAsync();

        return rows.GroupBy(e => e.AccountId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.BondDetailsId));
    }

    public async Task<BondAccountEntry?> GetOldest(int accountId) => await context.BondEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .LastOrDefaultAsync();

    public async Task<BondAccountEntry?> GetYoungest(int accountId) => await context.BondEntries
            .AsNoTracking()
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

    public Task RecalculateValues(int accountId, int entryId) =>
        RecalculateValues(accountId, entryId, CancellationToken.None);

    public async Task RecalculateValues(int accountId, int entryId, CancellationToken cancellationToken)
    {
        var startDate = await context.BondEntries
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
        // Recalculate from the oldest entry: every bond's anchor before it is empty, so each instrument
        // is rebuilt from a zero running balance.
        var startDate = await context.BondEntries
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

    public async Task<bool> AddLabel(int entryId, int labelId)
    {
        var entry = await context.BondEntries
            .Include(e => e.Labels)
            .FirstOrDefaultAsync(e => e.EntryId == entryId);
        var label = await context.FinancialLabels.FirstOrDefaultAsync(l => l.Id == labelId);

        if (entry is null || label is null) return false;

        // Idempotent: don't add a label the entry already carries.
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
        var entries = await context.BondEntries
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

    public async Task<IReadOnlyList<BondAccountEntry>> GetByIds(IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default)
    {
        if (entryIds.Count == 0)
            return [];

        return await context.BondEntries
            .AsNoTracking()
            .Where(e => entryIds.Contains(e.EntryId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BondAccountEntry>> GetMostRecentByAccounts(
        IReadOnlyCollection<int> accountIds,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0 || count <= 0)
            return [];

        List<BondAccountEntry> results = [];
        foreach (var accountIdChunk in accountIds.Distinct().Chunk(_maxAccountIdsPerQuery))
        {
            results.AddRange(await context.BondEntries
                .AsNoTracking()
                .Where(e => accountIdChunk.Contains(e.AccountId))
                .OrderByDescending(e => e.PostingDate)
                .ThenByDescending(e => e.EntryId)
                // The dashboard only needs account, ordering, value-change, and bond id fields.
                .Select(e => new BondAccountEntry(e.AccountId, e.EntryId, e.PostingDate, 0, e.ValueChange, e.BondDetailsId))
                .Take(count)
                .ToListAsync(cancellationToken));
        }

        return results
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.EntryId)
            .Take(count)
            .ToList();
    }

    public Task<IReadOnlyList<BondAccountEntry>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BondAccountEntry>>([]);

    async Task<Dictionary<int, BondAccountEntry>> IBondAccountEntryRepository<BondAccountEntry>.GetNextOlder(int accountId, DateTime date)
    {
        var byAccount = await GetNextOlderPerInstrument([accountId], date);
        return byAccount.GetValueOrDefault(accountId) ?? [];
    }

    async Task<Dictionary<int, BondAccountEntry>> IBondAccountEntryRepository<BondAccountEntry>.GetNextYounger(int accountId, DateTime date)
    {
        var byAccount = await GetNextYoungerPerInstrument([accountId], date);
        return byAccount.GetValueOrDefault(accountId) ?? [];
    }


}