using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Imports;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Transactions.Import;

public class BondImportService(
    ITransactionEntryRepository<BondEntry> bondAccountEntryRepository,
    ImportEntryLimitValidator importEntryLimitValidator,
    ILogger<BondImportService> logger) : IBondImportService
{
    // Keep each persistence operation bounded so a large import can make progress across
    // independently committed batches and does not create an unbounded EF change tracker.
    private const int _persistenceBatchSize = 500;

    public async Task<BondImportResult> ImportOwnerEntries(int userId, IEnumerable<BondEntryImport> entries, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        ArgumentNullException.ThrowIfNull(entries);
        cancellationToken.ThrowIfCancellationRequested();

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(0, 0, [], []);

        await importEntryLimitValidator.EnsureWithinPlanLimit(userId, entryList.Count);

        var minDay = entryList.Min(x => x.PostingDate).Date;
        var maxDay = entryList.Max(x => x.PostingDate).Date;

        int imported = 0;
        int failed = 0;
        var errors = new List<string>();
        var conflicts = new List<BondImportConflict>();

        var existingEntries = await GetOwnerEntries(userId, entryList.Select(x => x.BondDetailsId), minDay, maxDay, cancellationToken);
        var importsByDay = entryList
            .GroupBy(x => (Day: x.PostingDate.Date, Partition: x.BondDetailsId))
            .ToDictionary(g => g.Key, g => g.ToList());
        var existingByDay = existingEntries
            .GroupBy(e => (Day: e.PostingDate.Date, Partition: e.BondDetailsId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Newest day first, as before; within a day, one review group per partition.
        foreach (var group in importsByDay.OrderByDescending(g => g.Key.Day).ThenBy(g => g.Key.Partition))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var importsThisDay = group.Value;
            var existingThisDay = existingByDay.GetValueOrDefault(group.Key) ?? [];

            var exactMatches = ImportConflictDetector.GetExactMatches(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();
            var importsOnly = ImportConflictDetector.GetImportsMissingFromExisting(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();

            // Only a real overlap needs review; unrelated rows already on the day never block an import.
            if (exactMatches.Count != 0)
            {
                conflicts.AddRange(exactMatches.Select(x => new BondImportConflict(x.Import, x.Existing, "Exact match")));
                conflicts.AddRange(importsOnly.Select(x => new BondImportConflict(x, null, "Import not found in existing")));
                continue;
            }

            var entriesToInsert = new List<BondEntry>(importsThisDay.Count);
            foreach (var import in importsThisDay)
            {
                try
                {
                    if (import.PostingDate.Kind != DateTimeKind.Utc)
                        throw new Exception($"Date kind of this entry posting date: {import.PostingDate}, value change: {import.ValueChange} is not UTC - {import.PostingDate.Kind}");

                    entriesToInsert.Add(NewEntry(userId, import));
                }
                catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Bond account import cancelled.");
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    failed++;
                    errors.Add($"Cancellation while importing entry with date {import.PostingDate}.");
                    logger.LogDebug(ex, "Bond account import entry cancelled or timed out for {PostingDate}; marking it failed.", import.PostingDate);
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add(ex.Message);
                }
            }

            foreach (var batch in entriesToInsert.Chunk(_persistenceBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batchList = batch.ToList();
                try
                {
                    var added = await bondAccountEntryRepository.Add(batchList, cancellationToken);

                    if (added)
                    {
                        imported += batchList.Count;
                    }
                    else
                    {
                        failed += batchList.Count;
                        errors.AddRange(batchList.Select(entry => $"Failed to import entry with date {entry.PostingDate}."));
                    }
                }
                catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Bond account import cancelled.");
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    failed += batchList.Count;
                    errors.AddRange(batchList.Select(entry => $"Cancellation while importing entry with date {entry.PostingDate}."));
                    logger.LogDebug(ex, "Bond account import entries cancelled or timed out; marking them failed.");
                }
                catch (Exception ex)
                {
                    failed += batchList.Count;
                    errors.AddRange(batchList.Select(_ => ex.Message));
                }
            }
        }

        return new(imported, failed, errors, conflicts);
    }

    public async Task<BondImportResult> ApplyOwnerConflicts(int userId, IEnumerable<BondEntryImport> acceptedEntries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceptedEntries);
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        cancellationToken.ThrowIfCancellationRequested();

        var accepted = acceptedEntries.OrderBy(e => e.PostingDate).ToList();
        if (accepted.Count == 0)
            return new(0, 0, [], []);

        var errors = new List<string>();
        var failed = 0;
        var valid = new List<BondEntryImport>(accepted.Count);
        foreach (var entry in accepted)
        {
            if (entry.PostingDate.Kind != DateTimeKind.Utc)
            {
                failed++;
                errors.Add($"Date kind of this entry posting date: {entry.PostingDate}, value change: {entry.ValueChange} is not UTC - {entry.PostingDate.Kind}");
            }
            else
                valid.Add(entry);
        }

        // Only the still-missing occurrences are added: per key, the surplus of the accepted rows over what the owner has.
        var existing = valid.Count == 0
            ? []
            : await GetOwnerEntries(userId, valid.Select(x => x.BondDetailsId), valid.Min(x => x.PostingDate).Date, valid.Max(x => x.PostingDate).Date, cancellationToken);
        var existingCounts = existing.CountBy(ExistingKey).ToDictionary();
        var toAdd = valid
            .GroupBy(ImportKey)
            .SelectMany(group => group.Skip(existingCounts.GetValueOrDefault(group.Key)))
            .ToList();
        if (toAdd.Count == 0)
            return new(0, failed, errors, []);

        // The quota covers what is actually added, not the confirmed rows that already exist.
        await importEntryLimitValidator.EnsureWithinPlanLimit(userId, toAdd.Count);

        var imported = 0;
        foreach (var batch in toAdd.Chunk(_persistenceBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batchList = batch.Select(import => NewEntry(userId, import)).ToList();
            try
            {
                if (await bondAccountEntryRepository.Add(batchList, cancellationToken))
                    imported += batchList.Count;
                else
                {
                    failed += batchList.Count;
                    errors.AddRange(batchList.Select(entry => $"Failed to import entry with date {entry.PostingDate}."));
                }
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Applying resolved bond import conflicts cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                failed += batchList.Count;
                errors.AddRange(batchList.Select(_ => ex.Message));
            }
        }

        return new(imported, failed, errors, []);
    }

    // The owner's rows of each named bond definition between the first and last UTC day, whatever their link.
    private async Task<List<BondEntry>> GetOwnerEntries(
        int userId, IEnumerable<int> bondDetailsIds, DateTime minDay, DateTime maxDay, CancellationToken cancellationToken)
    {
        var start = DateTime.SpecifyKind(minDay, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(maxDay.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        List<BondEntry> entries = [];
        foreach (var bondDetailsId in bondDetailsIds.Distinct())
            entries.AddRange(await bondAccountEntryRepository.GetByOwner(userId, bondDetailsId, start, end, cancellationToken).ToListAsync(cancellationToken));
        return entries;
    }

    private static BondEntry NewEntry(int userId, BondEntryImport import) =>
        new(0, ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange, import.BondDetailsId)
        {
            UserId = userId,
            Description = import.Description
        };

    // Bond entries are compared on posting date (second precision), value change and bond details.
    private static (DateTime Date, decimal ValueChange, int BondDetailsId) ImportKey(BondEntryImport import) =>
        (ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange, import.BondDetailsId);

    private static (DateTime Date, decimal ValueChange, int BondDetailsId) ExistingKey(BondEntry entry) =>
        (ImportDateNormalizer.ToSecond(entry.PostingDate), entry.ValueChange, entry.BondDetailsId);
}