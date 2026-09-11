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
        IEnumerable<BondEntryImport> entries) =>
        ImportEntries(userId, accountId, entries, CancellationToken.None);

    public async Task<BondImportResult> ImportEntries(
        int userId,
        int accountId,
        IEnumerable<BondEntryImport> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        cancellationToken.ThrowIfCancellationRequested();

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(accountId, 0, 0, [], []);

        await importAccountValidator.EnsureWithinPlanLimit(userId, entryList.Count);

        var account = cancellationToken.CanBeCanceled
            ? await bondAccountRepository.Get(accountId, cancellationToken)
            : await bondAccountRepository.Get(accountId);
        importAccountValidator.EnsureOwnership(account, userId);

        var minDay = entryList.Min(x => x.PostingDate).Date;
        var maxDay = entryList.Max(x => x.PostingDate).Date;

        int imported = 0;
        int failed = 0;
        var errors = new List<string>();
        var conflicts = new List<BondImportConflict>();

        var existingEntries = cancellationToken.CanBeCanceled
            ? await bondAccountEntryRepository
                .Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1), cancellationToken)
                .ToListAsync(cancellationToken)
            : await bondAccountEntryRepository
                .Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1))
                .ToListAsync();
        var importsByDay = entryList
            .GroupBy(x => x.PostingDate.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
        var existingByDay = existingEntries
            .GroupBy(e => e.PostingDate.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
        BondAccountEntry? oldestInserted = null;

        try
        {
            for (var day = maxDay; day >= minDay; day = day.AddDays(-1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!importsByDay.TryGetValue(day, out var importsThisDay))
                    continue;
                var existingThisDay = existingByDay.GetValueOrDefault(day) ?? [];

                var exactMatches = ImportConflictDetector.GetExactMatches(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();
                var importsOnly = ImportConflictDetector.GetImportsMissingFromExisting(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();
                var existingOnly = ImportConflictDetector.GetExistingMissingFromImports(existingThisDay, importsThisDay, ExistingKey, ImportKey).ToList();

                if (exactMatches.Count != 0 || existingOnly.Count != 0)
                {
                    conflicts.AddRange(exactMatches.Select(x => new BondImportConflict(accountId, x.Import, x.Existing, "Exact match")));
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

                        var newEntry = new BondAccountEntry(accountId, 0, ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange, import.ValueChange, import.BondDetailsId);
                        entriesToInsert.Add(newEntry);
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

            if (imported > 0)
            {
                try
                {
                    await RecalculateBonds(accountId, oldestInserted?.EntryId ?? 0, minDay, maxDay, cancellationToken);
                }
                catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Bond account import recalculation cancelled.");
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    await RecalculateCommittedEntries(accountId, oldestInserted);
                    failed++;
                    errors.Add("Recalculation cancelled or timed out.");
                    logger.LogDebug(ex, "Bond account import recalculation cancelled or timed out; marking it failed.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RecalculateCommittedEntries(accountId, oldestInserted);
            throw;
        }

        return new(accountId, imported, failed, errors, conflicts);
    }

    public Task ApplyResolvedConflicts(IEnumerable<ResolvedBondImportConflict> resolvedConflicts) =>
        ApplyResolvedConflicts(resolvedConflicts, CancellationToken.None);

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
                if (!resolvedConflict.LeaveExisting && resolvedConflict.ExistingId is int existingId)
                {
                    if (cancellationToken.CanBeCanceled)
                        await bondAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId, cancellationToken);
                    else
                        await bondAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId);
                }

                if (resolvedConflict.AddImported && resolvedConflict.ImportData is not null)
                {
                    var entry = resolvedConflict.ToEntry();
                    if (cancellationToken.CanBeCanceled)
                        await bondAccountEntryRepository.Add(entry, recalculate: true, cancellationToken);
                    else
                        await bondAccountEntryRepository.Add(entry, recalculate: true);
                }
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Applying resolved bond import conflict cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying resolved conflict for account {AccountId}", resolvedConflict.AccountId);
            }
        }
    }

    private Task RecalculateCommittedEntries(int accountId, BondAccountEntry? oldestInserted) =>
        oldestInserted is null
            ? Task.CompletedTask
            : bondAccountEntryRepository.RecalculateValues(accountId, oldestInserted.EntryId, CancellationToken.None);

    private async Task RecalculateBonds(int accountId, int anchorEntryId, DateTime minDay, DateTime maxDay, CancellationToken cancellationToken)
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

    private static (DateTime Date, decimal ValueChange, int BondDetailsId) ExistingKey(BondAccountEntry entry) =>
        (ImportDateNormalizer.ToSecond(entry.PostingDate), entry.ValueChange, entry.BondDetailsId);
}