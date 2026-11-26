using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;

public class CurrencyEntryRepository(AppDbContext context) : IAccountEntryRepository<CurrencyAccountEntry>
{
    private static readonly System.Reflection.PropertyInfo? _entryIdProperty =
        typeof(CurrencyAccountEntry).GetProperty(nameof(CurrencyAccountEntry.EntryId));

    public Task<bool> Add(CurrencyAccountEntry entry, CancellationToken cancellationToken = default) =>
        Add([entry], cancellationToken);

    public async Task<bool> Add(
        IEnumerable<CurrencyAccountEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var entryList = entries as IList<CurrencyAccountEntry> ?? entries.ToList();
        if (entryList.Count == 0) return true;
        if (entryList.Any(e => e.UserId == 0)) return false;

        // Re-resolve already-persisted labels to context-tracked instances in one query so EF reuses the
        // existing rows instead of trying to INSERT detached copies — the guest seeder reads labels via
        // AsNoTracking and attaches them to these new entries. #408
        var existingLabelIds = entryList.SelectMany(e => e.Labels).Where(l => l.Id != 0).Select(l => l.Id).Distinct().ToList();
        var trackedById = existingLabelIds.Count == 0
            ? []
            : await context.FinancialLabels.Where(l => existingLabelIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        List<(CurrencyAccountEntry Source, CurrencyAccountEntry Target)> pairs = new(entryList.Count);

        foreach (var entry in entryList)
        {
            CurrencyAccountEntry newEntry = new(null, 0, entry.PostingDate, 0, entry.ValueChange)
            {
                UserId = entry.UserId,
                CurrencyId = entry.CurrencyId,
                Description = entry.Description,
                ContractorDetails = entry.ContractorDetails,
                Labels = entry.Labels.Select(l => l.Id != 0 && trackedById.TryGetValue(l.Id, out var tracked) ? tracked : l).ToList(),
            };

            pairs.Add((entry, newEntry));
            context.CurrencyEntries.Add(newEntry);
        }

        await context.SaveChangesAsync(cancellationToken);
        foreach (var (source, target) in pairs)
        {
            if (source.EntryId == 0 && target.EntryId != 0)
                _entryIdProperty?.SetValue(source, target.EntryId);
        }

        context.ChangeTracker.Clear();
        return true;
    }

    public async Task<bool> DeleteByOwner(int userId, long entryId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var entryToDelete = await context.CurrencyEntries.FirstOrDefaultAsync(e => e.UserId == userId && e.EntryId == entryId, cancellationToken);
        if (entryToDelete is null) return false;

        context.CurrencyEntries.Remove(entryToDelete);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CurrencyAccountEntry?> GetByOwner(int userId, long entryId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        return await context.CurrencyEntries
            .AsNoTracking()
            .Include(entry => entry.Labels)
            .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.EntryId == entryId, cancellationToken);
    }

    public async IAsyncEnumerable<CurrencyAccountEntry> GetByOwner(
        int userId,
        int partitionId,
        DateTime startDate,
        DateTime endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        await foreach (var entry in context.CurrencyEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.CurrencyId == partitionId && x.PostingDate >= startDate && x.PostingDate <= endDate)
            .OrderBy(x => x.PostingDate)
            .ThenBy(x => x.EntryId)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            yield return entry;
        }
    }

    public async Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<int, int>();

        // The entry carries its owner, so linked and unlinked rows are each counted exactly once.
        var rows = await context.CurrencyEntries.AsNoTracking()
            .Where(entry => userIds.Contains(entry.UserId))
            .GroupBy(entry => entry.UserId)
            .Select(grouped => new { UserId = grouped.Key, Count = grouped.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.UserId, x => x.Count);
    }

    public async Task<bool> UpdateByOwner(int userId, CurrencyAccountEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var existingEntry = await context.CurrencyEntries.Include(x => x.Labels)
            .FirstOrDefaultAsync(e => e.UserId == userId && e.EntryId == entry.EntryId, cancellationToken);
        if (existingEntry is null) return false;

        var labelIds = entry.Labels.Select(label => label.Id).ToList();
        entry.Labels = await context.FinancialLabels.Where(label => labelIds.Contains(label.Id)).ToListAsync(cancellationToken);
        existingEntry.Update(entry);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> AddLabels(int userId, IEnumerable<(long entryId, int labelId)> labelAssignments, CancellationToken cancellationToken = default)
    {
        if (userId == 0) return 0;
        var assignments = labelAssignments.ToList();
        if (assignments.Count == 0) return 0;

        var entryIds = assignments.Select(a => a.entryId).Distinct().ToList();
        var labelIds = assignments.Select(a => a.labelId).Distinct().ToList();

        // Fetch all relevant entries and labels at once
        var entries = await context.CurrencyEntries
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

    public async Task<IReadOnlyList<CurrencyAccountEntry>> GetByIds(int userId, IReadOnlyCollection<long> entryIds, CancellationToken cancellationToken = default)
    {
        if (userId == 0 || entryIds.Count == 0)
            return [];

        return await context.CurrencyEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && entryIds.Contains(e.EntryId))
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