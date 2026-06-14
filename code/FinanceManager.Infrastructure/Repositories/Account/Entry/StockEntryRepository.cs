using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

public class StockEntryRepository(AppDbContext context) : IStockAccountEntryRepository<StockAccountEntry>
{
    public async Task<bool> Add(StockAccountEntry entry, bool recalculate = true)
    {
        StockAccountEntry newEntry = new(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange, entry.Isin, entry.InvestmentType)
        {
            Ticker = entry.Ticker
        };

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
            var newEntry = new StockAccountEntry(entry.AccountId, 0, entry.PostingDate, entry.Value, entry.ValueChange, entry.Isin, entry.InvestmentType)
            {
                Ticker = entry.Ticker,
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
        if (context.Database.IsRelational())
        {
            var deleted = await context.StockEntries
                .Where(e => e.AccountId == accountId)
                .ExecuteDeleteAsync();
            return deleted > 0;
        }

        var entries = await context.StockEntries.Where(e => e.AccountId == accountId).ToListAsync();
        context.StockEntries.RemoveRange(entries);
        await context.SaveChangesAsync();
        return true;
    }
    public IAsyncEnumerable<StockAccountEntry> Get(int accountId, DateTime startDate, DateTime endDate) => context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate >= startDate && e.PostingDate <= endDate)
            .AsAsyncEnumerable();

    public async Task<List<DateTime>> GetPostingDates(int accountId) => await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .Select(e => e.PostingDate)
            .ToListAsync();

    public async Task<List<StockAccountEntry>> Get(int accountId, DateTime date, int count, bool olderThenDate = true)
    {
        if (count <= 0) return [];

        if (olderThenDate)
        {
            return await context.StockEntries
                .AsNoTracking()
                .Where(e => e.AccountId == accountId && e.PostingDate <= date)
                .OrderByDescending(e => e.PostingDate)
                .ThenByDescending(e => e.EntryId)
                .Take(count)
                .ToListAsync();
        }

        var entries = await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate >= date)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.EntryId)
            .Take(count)
            .ToListAsync();

        return entries
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.EntryId)
            .ToList();
    }

    public IAsyncEnumerable<StockAccountEntry> Get(int accountId, string isin, DateTime startDate, DateTime endDate) => context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.Isin == isin && e.PostingDate >= startDate && e.PostingDate <= endDate)
            .AsAsyncEnumerable();


    public Task<StockAccountEntry?> Get(int accountId, int entryId) =>
        context.StockEntries.AsNoTracking().SingleOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
    public Task<int> GetCount(int accountId) => context.StockEntries.AsNoTracking().CountAsync(e => e.AccountId == accountId);

    public async Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<int, int>();

        return await (
            from entry in context.StockEntries.AsNoTracking()
            join account in context.Accounts on entry.AccountId equals account.AccountId
            where userIds.Contains(account.UserId)
            group entry by account.UserId into grouped
            select new { UserId = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);
    }

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, int entryId)
    {
        var entry = await context.StockEntries.AsNoTracking().FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return null;

        return await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate < entry.PostingDate)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAccountEntry?> GetNextOlder(int accountId, DateTime date)
    {
        return await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate < date)
            .OrderByDescending(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// For each ISIN held by this account, returns the youngest entry strictly older than the given date.
    /// Uses a single grouped query to find all per-ISIN boundary dates, then resolves them in one fetch,
    /// eliminating the previous N+1 pattern of one query per ISIN.
    /// </summary>
    async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextOlder(int accountId, DateTime date)
    {
        var boundaries = await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate < date)
            .GroupBy(e => e.Isin)
            .Select(g => new { Isin = g.Key, Date = g.Max(e => e.PostingDate) })
            .ToListAsync();

        return await ResolveBoundaryEntries(accountId, boundaries.Select(b => (b.Isin, b.Date)).ToList());
    }

    public async Task<StockAccountEntry?> GetNextYounger(int accountId, int entryId)
    {
        var entry = await context.StockEntries.AsNoTracking().FirstOrDefaultAsync(e => e.AccountId == accountId && e.EntryId == entryId);
        if (entry is null) return null;
        return await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate > entry.PostingDate)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }

    public Task<StockAccountEntry?> GetNextYounger(int accountId, DateTime date) => context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate > date)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();

    public async Task<List<StockAccountEntry>> GetRange(IReadOnlyCollection<int> accountIds, DateTime startDate, DateTime endDate)
    {
        if (accountIds.Count == 0) return [];

        return await context.StockEntries
            .AsNoTracking()
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate >= startDate && e.PostingDate <= endDate)
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.EntryId)
            .ToListAsync();
    }

    public async Task<Dictionary<int, StockAccountEntry>> GetNextOlder(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.StockEntries
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate < date)
            .Where(e => !context.StockEntries.Any(o => o.AccountId == e.AccountId && o.PostingDate < date
                && (o.PostingDate > e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId > e.EntryId))))
            .ToListAsync();

        return rows.ToDictionary(e => e.AccountId);
    }

    public async Task<Dictionary<int, StockAccountEntry>> GetNextYounger(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.StockEntries
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate > date)
            .Where(e => !context.StockEntries.Any(o => o.AccountId == e.AccountId && o.PostingDate > date
                && (o.PostingDate < e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId < e.EntryId))))
            .ToListAsync();

        return rows.ToDictionary(e => e.AccountId);
    }

    public async Task<Dictionary<int, Dictionary<string, StockAccountEntry>>> GetNextOlderPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        // One row per (account, ISIN): the entry no other older-than-date entry of the same account+ISIN beats.
        var rows = await context.StockEntries
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate < date)
            .Where(e => !context.StockEntries.Any(o => o.AccountId == e.AccountId && o.Isin == e.Isin && o.PostingDate < date
                && (o.PostingDate > e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId > e.EntryId))))
            .ToListAsync();

        return rows.GroupBy(e => e.AccountId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.Isin));
    }

    public async Task<Dictionary<int, Dictionary<string, StockAccountEntry>>> GetNextYoungerPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date)
    {
        if (accountIds.Count == 0) return [];

        var rows = await context.StockEntries
            .Where(e => accountIds.Contains(e.AccountId) && e.PostingDate > date)
            .Where(e => !context.StockEntries.Any(o => o.AccountId == e.AccountId && o.Isin == e.Isin && o.PostingDate > date
                && (o.PostingDate < e.PostingDate || (o.PostingDate == e.PostingDate && o.EntryId < e.EntryId))))
            .ToListAsync();

        return rows.GroupBy(e => e.AccountId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.Isin));
    }


    /// <summary>
    /// For each ISIN held by this account, returns the oldest entry strictly younger than the given date.
    /// Uses a single grouped query scoped by AccountId to find all per-ISIN boundary dates, then resolves
    /// them in one fetch. The AccountId filter is essential to prevent leaking across all accounts' ISINs.
    /// Previously only applied at the inner query, causing the ISIN list to be sourced from the entire table.
    /// </summary>
    async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextYounger(int accountId, DateTime date)
    {
        var boundaries = await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate > date)
            .GroupBy(e => e.Isin)
            .Select(g => new { Isin = g.Key, Date = g.Min(e => e.PostingDate) })
            .ToListAsync();

        return await ResolveBoundaryEntries(accountId, boundaries.Select(b => (b.Isin, b.Date)).ToList());
    }

    /// <summary>
    /// Resolves a list of (ISIN, boundary date) pairs to their corresponding entries with a single fetch,
    /// avoiding N+1 round trips. Groups candidate rows by (ISIN, PostingDate) for O(1) lookup.
    /// </summary>
    private async Task<Dictionary<string, StockAccountEntry>> ResolveBoundaryEntries(
        int accountId,
        IReadOnlyList<(string Isin, DateTime Date)> boundaries)
    {
        Dictionary<string, StockAccountEntry> result = [];
        if (boundaries.Count == 0) return result;

        var dates = boundaries.Select(b => b.Date).Distinct().ToList();

        var rows = await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && dates.Contains(e.PostingDate))
            .ToListAsync();

        var byIsinDate = rows
            .GroupBy(e => (e.Isin, e.PostingDate))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var (isin, date) in boundaries)
        {
            if (byIsinDate.TryGetValue((isin, date), out var entry))
                result[isin] = entry;
        }

        return result;
    }

    public async Task<StockAccountEntry?> GetOldest(int accountId)
    {
        return await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefaultAsync();
    }
    public async Task<StockAccountEntry?> GetYoungest(int accountId)
    {
        return await context.StockEntries
            .AsNoTracking()
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

    public async Task RecalculateValues(int accountId)
    {
        // Each ISIN holds an independent running balance, so recalculate every instrument separately
        // from its own oldest entry (which anchors at a zero running balance).
        var isins = await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .Select(e => e.Isin)
            .Distinct()
            .ToListAsync();

        foreach (var isin in isins)
        {
            var oldestEntryId = await context.StockEntries
                .AsNoTracking()
                .Where(e => e.AccountId == accountId && e.Isin == isin)
                .OrderBy(e => e.PostingDate)
                .ThenBy(e => e.EntryId)
                .Select(e => (int?)e.EntryId)
                .FirstOrDefaultAsync();

            if (oldestEntryId is int entryId)
                await RecalculateValues(accountId, entryId);
        }
    }

    public async Task RecalculateValues(int accountId, int entryId)
    {
        var entryInfo = await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.EntryId == entryId)
            .Select(e => new { e.PostingDate, e.Isin })
            .FirstOrDefaultAsync();

        if (entryInfo is null) return;

        var anchor = await context.StockEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.Isin == entryInfo.Isin && e.PostingDate < entryInfo.PostingDate)
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.EntryId)
            .Select(e => (decimal?)e.Value)
            .FirstOrDefaultAsync();

        if (!context.Database.IsRelational())
        {
            var entries = await context.StockEntries
                .Where(e => e.AccountId == accountId && e.Isin == entryInfo.Isin && e.PostingDate >= entryInfo.PostingDate)
                .OrderBy(e => e.PostingDate)
                .ThenBy(e => e.EntryId)
                .ToListAsync();

            decimal running = anchor ?? 0m;
            foreach (var e in entries)
            {
                running += e.ValueChange;
                e.Value = running;
            }
            await context.SaveChangesAsync();
            return;
        }

        if (context.Database.ProviderName?.StartsWith("Npgsql") == true)
        {
            await context.Database.ExecuteSqlAsync($"""
                WITH running AS (
                    SELECT "EntryId",
                           {anchor ?? 0m} + SUM("ValueChange") OVER (
                               ORDER BY "PostingDate", "EntryId"
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                           ) AS "NewValue"
                    FROM "StockEntries"
                    WHERE "AccountId" = {accountId}
                      AND "Isin" = {entryInfo.Isin}
                      AND "PostingDate" >= {entryInfo.PostingDate}
                )
                UPDATE "StockEntries" AS e
                SET "Value" = r."NewValue"
                FROM running AS r
                WHERE e."EntryId" = r."EntryId"
                """);
        }
        else
        {
            await context.Database.ExecuteSqlAsync($"""
                WITH running AS (
                    SELECT EntryId,
                           {anchor ?? 0m} + SUM(ValueChange) OVER (
                               ORDER BY PostingDate, EntryId
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                           ) AS NewValue
                    FROM StockEntries
                    WHERE AccountId = {accountId}
                      AND Isin = {entryInfo.Isin}
                      AND PostingDate >= {entryInfo.PostingDate}
                )
                UPDATE e
                SET Value = r.NewValue
                FROM StockEntries AS e
                INNER JOIN running AS r ON e.EntryId = r.EntryId
                """);
        }
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
            .AsNoTracking()
            .Where(e => entryIds.Contains(e.EntryId))
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<StockAccountEntry>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StockAccountEntry>>([]);
}