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
        if (context.Database.IsRelational())
        {
            var deleted = await context.BondEntries
                .Where(e => e.AccountId == accountId)
                .ExecuteDeleteAsync();
            return deleted > 0;
        }

        var entriesToRemove = await context.BondEntries.Where(e => e.AccountId == accountId).ToListAsync();
        context.BondEntries.RemoveRange(entriesToRemove);
        await context.SaveChangesAsync();
        return true;
    }

    public IAsyncEnumerable<BondAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) => context.BondEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .Include(x => x.Labels)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .AsAsyncEnumerable();

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
            return (rangeEntries, oldestDate);
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

    public async Task<Dictionary<int, BondAccountEntry>> GetNextOlder(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.BondEntries
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

    public async Task RecalculateValues(int accountId, int entryId)
    {
        var startDate = await context.BondEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.EntryId == entryId)
            .Select(e => (DateTime?)e.PostingDate)
            .FirstOrDefaultAsync();

        if (startDate is not DateTime date) return;

        await RecalculateValues(accountId, date);
    }

    public async Task RecalculateValues(int accountId)
    {
        // Recalculate from the oldest entry: every bond's anchor before it is empty, so each instrument
        // is rebuilt from a zero running balance.
        var startDate = await context.BondEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.EntryId)
            .Select(e => (DateTime?)e.PostingDate)
            .FirstOrDefaultAsync();

        if (startDate is not DateTime date) return;

        await RecalculateValues(accountId, date);
    }

    private async Task RecalculateValues(int accountId, DateTime startDate)
    {
        if (!context.Database.IsRelational())
        {
            await RecalculateValuesInMemory(accountId, startDate);
            return;
        }

        if (context.Database.ProviderName?.StartsWith("Npgsql") == true)
        {
            // DISTINCT ON picks the most-recent entry per BondDetailsId before startDate as the anchor.
            await context.Database.ExecuteSqlAsync($"""
                WITH anchors AS (
                    SELECT DISTINCT ON ("BondDetailsId") "BondDetailsId", "Value" AS "AnchorValue"
                    FROM "BondEntries"
                    WHERE "AccountId" = {accountId} AND "PostingDate" < {startDate}
                    ORDER BY "BondDetailsId", "PostingDate" DESC, "EntryId" DESC
                ),
                running AS (
                    SELECT e."EntryId",
                           COALESCE(a."AnchorValue", 0) + SUM(e."ValueChange") OVER (
                               PARTITION BY e."BondDetailsId"
                               ORDER BY e."PostingDate", e."EntryId"
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                           ) AS "NewValue"
                    FROM "BondEntries" e
                    LEFT JOIN anchors a ON a."BondDetailsId" = e."BondDetailsId"
                    WHERE e."AccountId" = {accountId} AND e."PostingDate" >= {startDate}
                )
                UPDATE "BondEntries" AS e
                SET "Value" = r."NewValue"
                FROM running AS r
                WHERE e."EntryId" = r."EntryId"
                """);
        }
        else
        {
            await context.Database.ExecuteSqlAsync($"""
                WITH anchors_raw AS (
                    SELECT BondDetailsId, Value AS AnchorValue,
                           ROW_NUMBER() OVER (PARTITION BY BondDetailsId ORDER BY PostingDate DESC, EntryId DESC) AS rn
                    FROM BondEntries
                    WHERE AccountId = {accountId} AND PostingDate < {startDate}
                ),
                anchors AS (
                    SELECT BondDetailsId, AnchorValue FROM anchors_raw WHERE rn = 1
                ),
                running AS (
                    SELECT e.EntryId,
                           COALESCE(a.AnchorValue, 0) + SUM(e.ValueChange) OVER (
                               PARTITION BY e.BondDetailsId
                               ORDER BY e.PostingDate, e.EntryId
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                           ) AS NewValue
                    FROM BondEntries e
                    LEFT JOIN anchors a ON a.BondDetailsId = e.BondDetailsId
                    WHERE e.AccountId = {accountId} AND e.PostingDate >= {startDate}
                )
                UPDATE e
                SET Value = r.NewValue
                FROM BondEntries e
                INNER JOIN running r ON e.EntryId = r.EntryId
                """);
        }
    }

    private async Task RecalculateValuesInMemory(int accountId, DateTime startDate)
    {
        var entries = await context.BondEntries
            .Where(e => e.AccountId == accountId && e.PostingDate >= startDate)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.EntryId)
            .ToListAsync();

        if (entries.Count == 0) return;

        var bondIds = entries.Select(e => e.BondDetailsId).Distinct().ToList();

        // Single query for all bond anchors instead of one per BondDetailsId group.
        var anchors = (await context.BondEntries
                .AsNoTracking()
                .Where(e => e.AccountId == accountId && bondIds.Contains(e.BondDetailsId) && e.PostingDate < startDate)
                .OrderByDescending(e => e.PostingDate)
                .ThenByDescending(e => e.EntryId)
                .ToListAsync())
            .GroupBy(e => e.BondDetailsId)
            .ToDictionary(g => g.Key, g => g.First().Value);

        foreach (var bondGroup in entries.GroupBy(e => e.BondDetailsId))
        {
            decimal running = anchors.TryGetValue(bondGroup.Key, out var anchor) ? anchor : 0m;
            foreach (var e in bondGroup.OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId))
            {
                running += e.ValueChange;
                e.Value = running;
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

    public Task<IReadOnlyList<BondAccountEntry>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BondAccountEntry>>([]);

    async Task<Dictionary<int, BondAccountEntry>> IBondAccountEntryRepository<BondAccountEntry>.GetNextOlder(int accountId, DateTime date)
    {
        Dictionary<int, BondAccountEntry> result = [];

        var bondDetailsIds = await context.BondEntries
                                .AsNoTracking()
                                .Where(e => e.AccountId == accountId)
                                .Select(m => m.BondDetailsId)
                                .Distinct()
                                .ToListAsync();

        foreach (var bondDetailsId in bondDetailsIds)
        {
            var nextOlder = await context.BondEntries
                   .AsNoTracking()
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
                                .AsNoTracking()
                                .Where(e => e.AccountId == accountId)
                                .Select(m => m.BondDetailsId)
                                .Distinct()
                                .ToListAsync();

        foreach (var bondDetailsId in bondDetailsIds)
        {
            var nextYounger = await context.BondEntries
                   .AsNoTracking()
                   .Where(e => e.BondDetailsId == bondDetailsId && e.AccountId == accountId && e.PostingDate > date)
                   .OrderByDescending(e => e.PostingDate).ThenByDescending(e => e.EntryId)
                   .LastOrDefaultAsync();

            if (nextYounger is null) continue;

            result.Add(bondDetailsId, nextYounger);
        }

        return result;
    }


}