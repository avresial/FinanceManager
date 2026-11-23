using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;

public class BondEntryRepository(AppDbContext context) : IBondAccountEntryRepository<BondAccountEntry>
{
    private readonly BondEntryValueCalculator _valueCalculator = new(context);
    private static readonly System.Reflection.PropertyInfo? _entryIdProperty =
        typeof(BondAccountEntry).GetProperty(nameof(BondAccountEntry.EntryId));

    public Task<bool> Add(BondAccountEntry entry, bool recalculate) =>
        Add(entry, recalculate, CancellationToken.None);

    public Task<bool> Add(BondAccountEntry entry, bool recalculate, CancellationToken cancellationToken) =>
        Add([entry], recalculate, cancellationToken);

    public Task<bool> Add(IEnumerable<BondAccountEntry> entries, bool recalculate = true) =>
        Add(entries, recalculate, CancellationToken.None);

    public async Task<bool> Add(
        IEnumerable<BondAccountEntry> entries,
        bool recalculate,
        CancellationToken cancellationToken)
    {
        var entryList = entries as IList<BondAccountEntry> ?? entries.ToList();
        if (entryList.Count == 0) return true;
        var accounts = await LegacyEntryAccounts.Resolve(context, AccountType.Bond, entryList.Select(e => e.AccountId), cancellationToken);
        if (accounts is null) return false;
        // An unlinked entry is owned by the server-stamped owner it carries.
        if (entryList.Any(e => e.AccountId is null && (e.UserId is not int owner || owner == 0))) return false;

        var existingLabelIds = entryList.SelectMany(e => e.Labels).Where(l => l.Id != 0).Select(l => l.Id).Distinct().ToList();
        var trackedById = existingLabelIds.Count == 0
            ? []
            : await context.FinancialLabels.Where(l => existingLabelIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        List<(BondAccountEntry Source, BondAccountEntry Target)> pairs = new(entryList.Count);

        foreach (var entry in entryList)
        {
            // Don't use entry.Value as it may be a placeholder
            // The correct value will be calculated during recalculation
            var newEntry = new BondAccountEntry(entry.AccountId, 0, DateTime.SpecifyKind(entry.PostingDate, DateTimeKind.Utc),
             0, entry.ValueChange, entry.BondDetailsId)
            {
                UserId = entry.AccountId is int accountId ? accounts[accountId].UserId : entry.UserId,
                Description = entry.Description,
                Labels = entry.Labels.Select(l => l.Id != 0 && trackedById.TryGetValue(l.Id, out var tracked) ? tracked : l).ToList(),
            };

            pairs.Add((entry, newEntry));
            context.BondEntries.Add(newEntry);
        }

        async Task RecalculateAccounts(CancellationToken token)
        {
            foreach (var group in pairs.Where(p => p.Target.AccountId is not null).GroupBy(p => p.Target.AccountId!.Value))
                await RecalculateValues(group.Key, group.MinBy(p => p.Target.PostingDate).Target.EntryId, token);
        }

        var hasLinkedEntries = pairs.Any(p => p.Target.AccountId is not null);
        await using var transaction = context.Database.IsRelational() && recalculate && hasLinkedEntries
            ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await RecalculateAccounts(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        foreach (var (source, target) in pairs)
        {
            if (source.EntryId == 0 && target.EntryId != 0)
                _entryIdProperty?.SetValue(source, target.EntryId);
        }
        if (recalculate && hasLinkedEntries && transaction is null)
        {
            try
            {
                await RecalculateAccounts(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The insert already committed. Finish the repair after a provider timeout.
                await RecalculateAccounts(CancellationToken.None);
            }
        }
        context.ChangeTracker.Clear();
        return true;
    }

    public Task<bool> Delete(int accountId, long entryId) =>
        Delete(accountId, entryId, CancellationToken.None);

    public async Task<bool> Delete(int accountId, long entryId, CancellationToken cancellationToken)
    {
        var entryToDelete = await context.BondEntries
            .FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId, cancellationToken);
        if (entryToDelete is null) return false;

        await Remove(entryToDelete, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteByOwner(int userId, long entryId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var entryToDelete = await context.BondEntries.FirstOrDefaultAsync(e => e.UserId == userId && e.EntryId == entryId, cancellationToken);
        if (entryToDelete is null) return false;

        await Remove(entryToDelete, cancellationToken);
        return true;
    }

    // Removes a tracked entry. A linked entry also repairs its account's running values; an unlinked one has none.
    private async Task Remove(BondAccountEntry entryToDelete, CancellationToken cancellationToken)
    {
        if (entryToDelete.AccountId is not int accountId)
        {
            context.BondEntries.Remove(entryToDelete);
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        // Relational: the deletion and the recalculation commit together.
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        context.BondEntries.Remove(entryToDelete);
        await context.SaveChangesAsync(cancellationToken);
        await RecalculateValues(accountId, entryToDelete.PostingDate, cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
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

    public async Task<BondAccountEntry?> Get(int accountId, long entryId) => await context.BondEntries
            .AsNoTracking()
            .Include(entry => entry.Labels)
            .FirstOrDefaultAsync(x => x.AccountId == accountId && x.EntryId == entryId);

    public async Task<BondAccountEntry?> GetByOwner(int userId, long entryId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        return await context.BondEntries
            .AsNoTracking()
            .Include(entry => entry.Labels)
            .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.EntryId == entryId, cancellationToken);
    }

    public async IAsyncEnumerable<BondAccountEntry> GetByOwner(
        int userId,
        int partitionId,
        DateTime startDate,
        DateTime endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        await foreach (var entry in context.BondEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.BondDetailsId == partitionId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .OrderBy(x => x.PostingDate)
            .ThenBy(x => x.EntryId)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            yield return entry;
        }
    }

    public async Task<int> GetCount(int accountId) => await context.BondEntries.AsNoTracking().CountAsync(x => x.AccountId == accountId);

    public async Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<int, int>();

        // The entry carries its owner, so linked and unlinked rows are each counted exactly once.
        var rows = await context.BondEntries.AsNoTracking()
            .Where(entry => entry.UserId != null && userIds.Contains(entry.UserId.Value))
            .GroupBy(entry => entry.UserId!.Value)
            .Select(grouped => new { UserId = grouped.Key, Count = grouped.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.UserId, x => x.Count);
    }

    public async Task<BondAccountEntry?> GetNextOlder(int accountId, long entryId)
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

    public async Task<BondAccountEntry?> GetNextYounger(int accountId, long entryId)
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
            .Where(x => x.AccountId.HasValue && accountIds.Contains(x.AccountId.Value) && x.PostingDate >= startDate && x.PostingDate <= endDate)
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
            .Where(x => x.AccountId.HasValue && accountIds.Contains(x.AccountId.Value) && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .ToListAsync();
    }

    public async Task<Dictionary<int, BondAccountEntry>> GetNextOlder(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.BondEntries
            .AsNoTracking()
            .Where(e => e.AccountId.HasValue && accountIds.Contains(e.AccountId.Value) && e.PostingDate < date)
            .Where(e => !context.BondEntries.Any(o => o.AccountId == e.AccountId && o.PostingDate < date
                && (o.PostingDate > e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId > e.EntryId))))
            .ToListAsync();

        return rows.ToDictionary(e => e.AccountId!.Value);
    }

    public async Task<Dictionary<int, BondAccountEntry>> GetNextYounger(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.BondEntries
            .AsNoTracking()
            .Where(e => e.AccountId.HasValue && accountIds.Contains(e.AccountId.Value) && e.PostingDate > date)
            .Where(e => !context.BondEntries.Any(o => o.AccountId == e.AccountId && o.PostingDate > date
                && (o.PostingDate < e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId < e.EntryId))))
            .ToListAsync();

        return rows.ToDictionary(e => e.AccountId!.Value);
    }

    public async Task<Dictionary<int, Dictionary<int, BondAccountEntry>>> GetNextOlderPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        // One row per (account, bond details id): the entry no other older-than-date entry of the same
        // account+bond beats on (PostingDate, EntryId).
        var rows = await context.BondEntries
            .AsNoTracking()
            .Where(e => e.AccountId.HasValue && accountIds.Contains(e.AccountId.Value) && e.PostingDate < date)
            .Where(e => !context.BondEntries.Any(o => o.AccountId == e.AccountId && o.BondDetailsId == e.BondDetailsId && o.PostingDate < date
                && (o.PostingDate > e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId > e.EntryId))))
            .ToListAsync();

        return rows.GroupBy(e => e.AccountId!.Value)
            .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.BondDetailsId));
    }

    public async Task<Dictionary<int, Dictionary<int, BondAccountEntry>>> GetNextYoungerPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.BondEntries
            .AsNoTracking()
            .Where(e => e.AccountId.HasValue && accountIds.Contains(e.AccountId.Value) && e.PostingDate > date)
            .Where(e => !context.BondEntries.Any(o => o.AccountId == e.AccountId && o.BondDetailsId == e.BondDetailsId && o.PostingDate > date
                && (o.PostingDate < e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId < e.EntryId))))
            .ToListAsync();

        return rows.GroupBy(e => e.AccountId!.Value)
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
        if (entry.AccountId is not int accountId) return false;
        var accounts = await LegacyEntryAccounts.Resolve(context, AccountType.Bond, [accountId], CancellationToken.None);
        if (accounts is null) return false;
        var existingEntry = await context.BondEntries.Include(x => x.Labels).FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entry.EntryId);
        if (existingEntry is null) return false;

        await ApplyUpdate(existingEntry, entry, CancellationToken.None, accounts[accountId].UserId);
        return true;
    }

    public async Task<bool> UpdateByOwner(int userId, BondAccountEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var existingEntry = await context.BondEntries.Include(x => x.Labels)
            .FirstOrDefaultAsync(e => e.UserId == userId && e.EntryId == entry.EntryId, cancellationToken);
        if (existingEntry is null) return false;

        // The owner and account link of the stored row stay as they are.
        await ApplyUpdate(existingEntry, entry, cancellationToken, linkedAccountOwner: null);
        return true;
    }

    private async Task ApplyUpdate(BondAccountEntry existingEntry, BondAccountEntry entry, CancellationToken cancellationToken, int? linkedAccountOwner)
    {
        List<FinancialLabel> newLabels = [];
        foreach (var label in entry.Labels)
        {
            var existingLabel = await context.FinancialLabels.FirstOrDefaultAsync(x => x.Id == label.Id, cancellationToken);
            if (existingLabel is null) continue;

            newLabels.Add(existingLabel);
        }

        entry.Labels = newLabels;

        var previousPostingDate = existingEntry.PostingDate;
        existingEntry.Update(entry);
        if (linkedAccountOwner is int owner)
            existingEntry.UserId = owner;

        if (existingEntry.AccountId is not int accountId)
        {
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        // A move to a later date, or to another bond, also changes the running values between the old and the new position.
        var recalculateFrom = previousPostingDate <= existingEntry.PostingDate ? previousPostingDate : existingEntry.PostingDate;
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        await context.SaveChangesAsync(cancellationToken);
        await RecalculateValues(accountId, recalculateFrom, cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
    }

    public Task RecalculateValues(int accountId, long entryId) =>
        RecalculateValues(accountId, entryId, CancellationToken.None);

    public async Task RecalculateValues(int accountId, long entryId, CancellationToken cancellationToken)
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

    public async Task<int> AddLabels(int userId, IEnumerable<(long entryId, int labelId)> labelAssignments, CancellationToken cancellationToken = default)
    {
        if (userId == 0) return 0;
        var assignments = labelAssignments.ToList();
        if (assignments.Count == 0) return 0;

        var entryIds = assignments.Select(a => a.entryId).Distinct().ToList();
        var labelIds = assignments.Select(a => a.labelId).Distinct().ToList();

        // Fetch all relevant entries and labels at once
        var entries = await context.BondEntries
            .Where(e => e.UserId == userId && entryIds.Contains(e.EntryId))
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

    public async Task<IReadOnlyList<BondAccountEntry>> GetByIds(int userId, IReadOnlyCollection<long> entryIds, CancellationToken cancellationToken = default)
    {
        if (userId == 0 || entryIds.Count == 0)
            return [];

        return await context.BondEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && entryIds.Contains(e.EntryId))
            .ToListAsync(cancellationToken);
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