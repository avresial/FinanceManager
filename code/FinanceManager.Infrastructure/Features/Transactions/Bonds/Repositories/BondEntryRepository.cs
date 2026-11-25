using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;

public class BondEntryRepository(AppDbContext context) : ITransactionEntryRepository<BondEntry>
{
    private static readonly System.Reflection.PropertyInfo? _entryIdProperty =
        typeof(BondEntry).GetProperty(nameof(BondEntry.EntryId));

    public Task<bool> Add(BondEntry entry, CancellationToken cancellationToken = default) =>
        Add([entry], cancellationToken);

    public async Task<bool> Add(
        IEnumerable<BondEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var entryList = entries as IList<BondEntry> ?? entries.ToList();
        if (entryList.Count == 0) return true;
        if (entryList.Any(e => e.UserId == 0)) return false;

        var existingLabelIds = entryList.SelectMany(e => e.Labels).Where(l => l.Id != 0).Select(l => l.Id).Distinct().ToList();
        var trackedById = existingLabelIds.Count == 0
            ? []
            : await context.FinancialLabels.Where(l => existingLabelIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        List<(BondEntry Source, BondEntry Target)> pairs = new(entryList.Count);

        foreach (var entry in entryList)
        {
            var newEntry = new BondEntry(0, DateTime.SpecifyKind(entry.PostingDate, DateTimeKind.Utc), entry.ValueChange, entry.BondDetailsId)
            {
                UserId = entry.UserId,
                Description = entry.Description,
                Labels = entry.Labels.Select(l => l.Id != 0 && trackedById.TryGetValue(l.Id, out var tracked) ? tracked : l).ToList(),
            };

            pairs.Add((entry, newEntry));
            context.BondEntries.Add(newEntry);
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
        var entryToDelete = await context.BondEntries.FirstOrDefaultAsync(e => e.UserId == userId && e.EntryId == entryId, cancellationToken);
        if (entryToDelete is null) return false;

        context.BondEntries.Remove(entryToDelete);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BondEntry?> GetByOwner(int userId, long entryId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        return await context.BondEntries
            .AsNoTracking()
            .Include(entry => entry.Labels)
            .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.EntryId == entryId, cancellationToken);
    }

    public async IAsyncEnumerable<BondEntry> GetByOwner(
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

    public async Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<int, int>();

        // The entry carries its owner, so linked and unlinked rows are each counted exactly once.
        var rows = await context.BondEntries.AsNoTracking()
            .Where(entry => userIds.Contains(entry.UserId))
            .GroupBy(entry => entry.UserId)
            .Select(grouped => new { UserId = grouped.Key, Count = grouped.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.UserId, x => x.Count);
    }

    public async Task<bool> UpdateByOwner(int userId, BondEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var existingEntry = await context.BondEntries.Include(x => x.Labels)
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

    public async Task<IReadOnlyList<BondEntry>> GetByIds(int userId, IReadOnlyCollection<long> entryIds, CancellationToken cancellationToken = default)
    {
        if (userId == 0 || entryIds.Count == 0)
            return [];

        return await context.BondEntries
            .AsNoTracking()
            .Include(e => e.Labels)
            .Where(e => e.UserId == userId && entryIds.Contains(e.EntryId))
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<BondEntry>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BondEntry>>([]);
}