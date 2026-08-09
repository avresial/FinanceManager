using FinanceManager.Application.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Import;

public class CurrencyAccountImportService(ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyAccountEntryRepository,
    ImportAccountValidator importAccountValidator, ILogger<CurrencyAccountImportService> logger) : ICurrencyAccountImportService
{
    public Task<ImportResult> ImportEntries(
        int userId,
        int accountId,
        IEnumerable<CurrencyEntryImport> entries,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts = null,
        Func<int, int, int, Task>? onProgress = null) =>
        ImportEntries(userId, accountId, entries, CancellationToken.None, onConflicts, onProgress);

    public async Task<ImportResult> ImportEntries(
        int userId,
        int accountId,
        IEnumerable<CurrencyEntryImport> entries,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts = null,
        Func<int, int, int, Task>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        cancellationToken.ThrowIfCancellationRequested();

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(accountId, 0, 0, [], []);

        await importAccountValidator.EnsureWithinPlanLimit(userId, entryList.Count);

        var account = cancellationToken.CanBeCanceled
            ? await currencyAccountRepository.Get(accountId, cancellationToken)
            : await currencyAccountRepository.Get(accountId);
        importAccountValidator.EnsureOwnership(account, userId);

        var minDay = entryList.Min(x => x.PostingDate).Date;
        var maxDay = entryList.Max(x => x.PostingDate).Date;

        int imported = 0;
        int failed = 0;
        int processed = 0;
        var errors = new List<string>();
        var conflicts = new List<ImportConflict>();
        CurrencyAccountEntry? oldestInserted = null;

        var existingAll = cancellationToken.CanBeCanceled
            ? await currencyAccountEntryRepository
                .Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1), cancellationToken)
                .ToListAsync(cancellationToken)
            : await currencyAccountEntryRepository
                .Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1))
                .ToListAsync();
        for (var day = maxDay; day >= minDay; day = day.AddDays(-1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var importsThisDay = entryList.Where(x => x.PostingDate.Date == day).ToList();
            var existingThisDay = existingAll.Where(e => e.PostingDate.Date == day).ToList();

            if (importsThisDay.Count == 0) continue;

            var exactMatches = ImportConflictDetector.GetExactMatches(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();
            var importsOnly = ImportConflictDetector.GetImportsMissingFromExisting(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();
            var existingOnly = ImportConflictDetector.GetExistingMissingFromImports(existingThisDay, importsThisDay, ExistingKey, ImportKey).ToList();

            if (exactMatches.Count != 0 || existingOnly.Count != 0)
            {
                var dailyConflicts = new List<ImportConflict>();
                dailyConflicts.AddRange(exactMatches.Select(x => new ImportConflict(accountId, x.Import, x.Existing, "Exact match")));
                dailyConflicts.AddRange(importsOnly.Select(x => new ImportConflict(accountId, x, null, "Import not found in existing")));
                dailyConflicts.AddRange(existingOnly.Select(x => new ImportConflict(accountId, null, x, "Existing not found in import")));

                conflicts.AddRange(dailyConflicts);

                if (onConflicts is not null)
                    await onConflicts(dailyConflicts);

                processed += importsThisDay.Count;
                if (onProgress is not null)
                    await onProgress(processed, imported, failed);

                continue;
            }

            foreach (var import in importsThisDay)
            {
                try
                {
                    if (import.PostingDate.Kind != DateTimeKind.Utc)
                        throw new Exception($"Date kind of this entry posting date: {import.PostingDate}, value change: {import.ValueChange} is not UTC - {import.PostingDate.Kind}");

                    CurrencyAccountEntry newEntry = new(accountId, 0, ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange, import.ValueChange)
                    {
                        Description = import.Description ?? string.Empty,
                        ContractorDetails = import.ContractorDetails,
                        Labels = []
                    };

                    var added = cancellationToken.CanBeCanceled
                        ? await currencyAccountEntryRepository.Add(newEntry, recalculate: false, cancellationToken)
                        : await currencyAccountEntryRepository.Add(newEntry, recalculate: false);
                    if (added)
                    {
                        imported++;
                        existingAll.Add(newEntry);
                        if (oldestInserted is null || newEntry.PostingDate < oldestInserted.PostingDate)
                            oldestInserted = newEntry;
                    }
                    else
                    {
                        failed++;
                        errors.Add($"Failed to import entry with date {import.PostingDate}.");
                    }
                }
                catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Currency account import cancelled.");
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    failed++;
                    errors.Add($"Cancellation while importing entry with date {import.PostingDate}.");
                    logger.LogDebug(ex, "Currency account import entry cancelled or timed out for {PostingDate}; marking it failed.", import.PostingDate);
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add(ex.Message);
                }

            }

            processed += importsThisDay.Count;
            if (onProgress is not null)
                await onProgress(processed, imported, failed);
        }

        if (oldestInserted is not null)
        {
            try
            {
                if (cancellationToken.CanBeCanceled)
                    await currencyAccountEntryRepository.RecalculateValues(accountId, oldestInserted.EntryId, cancellationToken);
                else
                    await currencyAccountEntryRepository.RecalculateValues(accountId, oldestInserted.EntryId);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Currency account import recalculation cancelled.");
                throw;
            }
            catch (OperationCanceledException ex)
            {
                failed++;
                errors.Add("Recalculation cancelled or timed out.");
                logger.LogDebug(ex, "Currency account import recalculation cancelled or timed out; marking it failed.");
            }
        }

        return new(accountId, imported, failed, errors, conflicts);
    }

    public Task ApplyResolvedConflicts(IEnumerable<ResolvedImportConflict> resolvedConflicts) =>
        ApplyResolvedConflicts(resolvedConflicts, CancellationToken.None);

    public async Task ApplyResolvedConflicts(
        IEnumerable<ResolvedImportConflict> resolvedConflicts,
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
                    if (cancellationToken.CanBeCanceled)
                        await currencyAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId, cancellationToken);
                    else
                        await currencyAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId);

                if (resolvedConflict.AddImported && resolvedConflict.ImportData is not null)
                {
                    var importData = resolvedConflict.ImportData;
                    if (cancellationToken.CanBeCanceled)
                        await currencyAccountEntryRepository.Add(resolvedConflict.ToEntry(), recalculate: true, cancellationToken);
                    else
                        await currencyAccountEntryRepository.Add(resolvedConflict.ToEntry());
                }
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Applying resolved currency import conflict cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying resolved conflict for account {AccountId}", resolvedConflict.AccountId);
            }
        }
    }

    // Currency entries are compared on posting date (second precision) and value change.
    private static (DateTime Date, decimal ValueChange) ImportKey(CurrencyEntryImport import) =>
        (ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange);

    private static (DateTime Date, decimal ValueChange) ExistingKey(CurrencyAccountEntry entry) =>
        (ImportDateNormalizer.ToSecond(entry.PostingDate), entry.ValueChange);
}