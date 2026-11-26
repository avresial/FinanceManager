using FinanceManager.Application.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Bond.Import;

public class BondAccountImportService(
    IAccountRepository<BondAccount> bondAccountRepository,
    IBondAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository,
    ImportAccountValidator importAccountValidator,
    ILogger<BondAccountImportService> logger) : IBondAccountImportService
{
    // Keep each persistence operation bounded so a large import can make progress across
    // independently committed batches and does not create an unbounded EF change tracker.
    private const int _persistenceBatchSize = 500;

    public Task<BondImportResult> ImportEntries(
        int userId,
        int accountId,
        IEnumerable<BondEntryImport> entries,
        CancellationToken cancellationToken) =>
        Import(userId, accountId, entries, cancellationToken);

    public Task<BondImportResult> ImportOwnerEntries(int userId, IEnumerable<BondEntryImport> entries, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        return Import(userId, accountId: null, entries, cancellationToken);
    }

    // One import flow for both scopes. A concrete account id is the legacy account import; without one the
    // owner's pool is imported and nothing depends on a running value.
    private async Task<BondImportResult> Import(
        int userId,
        int? accountId,
        IEnumerable<BondEntryImport> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        cancellationToken.ThrowIfCancellationRequested();

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(accountId, 0, 0, [], []);

        await importAccountValidator.EnsureWithinPlanLimit(userId, entryList.Count);

        if (accountId is int linkedAccountId)
        {
            var account = cancellationToken.CanBeCanceled
                ? await bondAccountRepository.Get(linkedAccountId, cancellationToken)
                : await bondAccountRepository.Get(linkedAccountId);
            importAccountValidator.EnsureOwnership(account, userId);
        }

        var minDay = entryList.Min(x => x.PostingDate).Date;
        var maxDay = entryList.Max(x => x.PostingDate).Date;

        int imported = 0;
        int failed = 0;
        var errors = new List<string>();
        var conflicts = new List<BondImportConflict>();

        var existingEntries = accountId is int existingAccountId
            ? cancellationToken.CanBeCanceled
                ? await bondAccountEntryRepository
                    .Get(existingAccountId, minDay.AddDays(-1), maxDay.AddDays(1), cancellationToken)
                    .ToListAsync(cancellationToken)
                : await bondAccountEntryRepository
                    .Get(existingAccountId, minDay.AddDays(-1), maxDay.AddDays(1))
                    .ToListAsync()
            : await GetOwnerEntries(userId, entryList.Select(x => x.BondDetailsId), minDay, maxDay, cancellationToken);
        // A legacy account import reviews a whole day at once. An owner import keeps each bond definition apart:
        // rows of another definition on the same day neither trigger nor join a review.
        int PartitionOf(int bondDetailsId) => accountId is null ? bondDetailsId : 0;
        var importsByDay = entryList
            .GroupBy(x => (Day: x.PostingDate.Date, Partition: PartitionOf(x.BondDetailsId)))
            .ToDictionary(g => g.Key, g => g.ToList());
        var existingByDay = existingEntries
            .GroupBy(e => (Day: e.PostingDate.Date, Partition: PartitionOf(e.BondDetailsId)))
            .ToDictionary(g => g.Key, g => g.ToList());
        BondAccountEntry? oldestInserted = null;

        try
        {
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
                    // Existing-only rows may come from unrelated statements, so they are reviewed in a legacy account import only.
                    var existingOnly = accountId is null
                        ? []
                        : ImportConflictDetector.GetExistingMissingFromImports(existingThisDay, importsThisDay, ExistingKey, ImportKey).ToList();
                    conflicts.AddRange(exactMatches.Select(x => new BondImportConflict(accountId, x.Import, accountId is null ? WithoutLink(x.Existing) : x.Existing, "Exact match")));
                    conflicts.AddRange(importsOnly.Select(x => new BondImportConflict(accountId, x, null, "Import not found in existing")));
                    conflicts.AddRange(existingOnly.Select(x => new BondImportConflict(accountId, null, x, "Existing not found in import")));
                    continue;
                }

                var entriesToInsert = new List<BondAccountEntry>(importsThisDay.Count);
                foreach (var import in importsThisDay)
                {
                    try
                    {
                        if (import.PostingDate.Kind != DateTimeKind.Utc)
                            throw new Exception($"Date kind of this entry posting date: {import.PostingDate}, value change: {import.ValueChange} is not UTC - {import.PostingDate.Kind}");

                        entriesToInsert.Add(NewEntry(userId, accountId, import));
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
                        var added = cancellationToken.CanBeCanceled
                            ? await bondAccountEntryRepository.Add(batchList, recalculate: false, cancellationToken)
                            : await bondAccountEntryRepository.Add(batchList, recalculate: false);

                        if (added)
                        {
                            imported += batchList.Count;
                            var batchOldest = batchList.MinBy(entry => (entry.PostingDate, entry.EntryId))!;
                            if (oldestInserted is null || batchOldest.PostingDate < oldestInserted.PostingDate ||
                                batchOldest.PostingDate == oldestInserted.PostingDate && batchOldest.EntryId < oldestInserted.EntryId)
                                oldestInserted = batchOldest;
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

            if (accountId is int recalculatedAccountId && imported > 0)
            {
                try
                {
                    await RecalculateBonds(recalculatedAccountId, oldestInserted?.EntryId ?? 0, minDay, maxDay, cancellationToken);
                }
                catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Bond account import recalculation cancelled.");
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    await RecalculateCommittedEntries(recalculatedAccountId, oldestInserted);
                    failed++;
                    errors.Add("Recalculation cancelled or timed out.");
                    logger.LogDebug(ex, "Bond account import recalculation cancelled or timed out; marking it failed.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (accountId is int canceledAccountId)
                await RecalculateCommittedEntries(canceledAccountId, oldestInserted);
            throw;
        }

        return new(accountId, imported, failed, errors, conflicts);
    }

    public async Task<BondImportResult> ApplyOwnerConflicts(int userId, IEnumerable<BondEntryImport> acceptedEntries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceptedEntries);
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        cancellationToken.ThrowIfCancellationRequested();

        var accepted = acceptedEntries.OrderBy(e => e.PostingDate).ToList();
        if (accepted.Count == 0)
            return new(null, 0, 0, [], []);

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
            return new(null, 0, failed, errors, []);

        // The quota covers what is actually added, not the confirmed rows that already exist.
        await importAccountValidator.EnsureWithinPlanLimit(userId, toAdd.Count);

        var imported = 0;
        foreach (var batch in toAdd.Chunk(_persistenceBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batchList = batch.Select(import => NewEntry(userId, accountId: null, import)).ToList();
            try
            {
                if (await bondAccountEntryRepository.Add(batchList, recalculate: false, cancellationToken))
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

        return new(null, imported, failed, errors, []);
    }

    // The owner's rows of each named bond definition between the first and last UTC day, whatever their link.
    private async Task<List<BondAccountEntry>> GetOwnerEntries(
        int userId, IEnumerable<int> bondDetailsIds, DateTime minDay, DateTime maxDay, CancellationToken cancellationToken)
    {
        var start = DateTime.SpecifyKind(minDay, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(maxDay.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        List<BondAccountEntry> entries = [];
        foreach (var bondDetailsId in bondDetailsIds.Distinct())
            entries.AddRange(await bondAccountEntryRepository.GetByOwner(userId, bondDetailsId, start, end, cancellationToken).ToListAsync(cancellationToken));
        return entries;
    }

    // A linked entry is stamped by the repository from its account and carries the running value; an owner entry
    // carries the validated owner and keeps no running value.
    private static BondAccountEntry NewEntry(int userId, int? accountId, BondEntryImport import) =>
        new(accountId, 0, ImportDateNormalizer.ToSecond(import.PostingDate), accountId is null ? 0 : import.ValueChange, import.ValueChange, import.BondDetailsId)
        {
            UserId = accountId is null ? userId : null,
            Description = import.Description
        };

    public async Task ApplyResolvedConflicts(
        IEnumerable<ResolvedBondImportConflict> resolvedConflicts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolvedConflicts);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var resolvedConflict in resolvedConflicts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!resolvedConflict.LeaveExisting && resolvedConflict.ExistingId is long existingId)
                {
                    if (cancellationToken.CanBeCanceled)
                        await bondAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId, cancellationToken);
                    else
                        await bondAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId);
                }

                if (resolvedConflict.AddImported && resolvedConflict.ImportData is not null)
                {
                    var entry = resolvedConflict.ToEntry();
                    var added = cancellationToken.CanBeCanceled
                        ? await bondAccountEntryRepository.Add(entry, recalculate: true, cancellationToken)
                        : await bondAccountEntryRepository.Add(entry, recalculate: true);
                    if (!added) logger.LogError("Could not add a resolved bond import entry.");
                }
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Applying resolved bond import conflict cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying resolved bond import conflict.");
            }
        }
    }

    private Task RecalculateCommittedEntries(int accountId, BondAccountEntry? oldestInserted) =>
        oldestInserted is null
            ? Task.CompletedTask
            : bondAccountEntryRepository.RecalculateValues(accountId, oldestInserted.EntryId, CancellationToken.None);

    private async Task RecalculateBonds(int accountId, long anchorEntryId, DateTime minDay, DateTime maxDay, CancellationToken cancellationToken)
    {
        if (anchorEntryId != 0)
        {
            if (cancellationToken.CanBeCanceled)
                await bondAccountEntryRepository.RecalculateValues(accountId, anchorEntryId, cancellationToken);
            else
                await bondAccountEntryRepository.RecalculateValues(accountId, anchorEntryId);
            return;
        }

        var entriesToRecalc = cancellationToken.CanBeCanceled
            ? await bondAccountEntryRepository
                .Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1), cancellationToken)
                .ToListAsync(cancellationToken)
            : await bondAccountEntryRepository
                .Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1))
                .ToListAsync();

        var earliest = entriesToRecalc.OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId).FirstOrDefault();
        if (earliest is null)
            return;

        if (cancellationToken.CanBeCanceled)
            await bondAccountEntryRepository.RecalculateValues(accountId, earliest.EntryId, cancellationToken);
        else
            await bondAccountEntryRepository.RecalculateValues(accountId, earliest.EntryId);
    }

    // Bond entries are compared on posting date (second precision), value change and bond details.
    private static (DateTime Date, decimal ValueChange, int BondDetailsId) ImportKey(BondEntryImport import) =>
        (ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange, import.BondDetailsId);

    // An owner review shows the existing row without its legacy account link or running value.
    private static BondAccountEntry WithoutLink(BondAccountEntry entry) =>
        new(null, entry.EntryId, entry.PostingDate, 0, entry.ValueChange, entry.BondDetailsId)
        {
            UserId = entry.UserId,
            Description = entry.Description,
            Labels = entry.Labels
        };

    private static (DateTime Date, decimal ValueChange, int BondDetailsId) ExistingKey(BondAccountEntry entry) =>
        (ImportDateNormalizer.ToSecond(entry.PostingDate), entry.ValueChange, entry.BondDetailsId);
}