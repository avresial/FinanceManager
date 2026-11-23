using FinanceManager.Application.FinancialAccounts.Shared.Imports;
using FinanceManager.Application.TransactionRules;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Import;

public class CurrencyAccountImportService(ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyAccountEntryRepository,
    ImportAccountValidator importAccountValidator, ILogger<CurrencyAccountImportService> logger,
    ITransactionRuleService? transactionRuleService = null) : ICurrencyAccountImportService
{
    // Keep each persistence operation bounded so large imports retain partial-failure semantics and
    // do not create an unbounded EF change tracker or provider command batch.
    private const int _persistenceBatchSize = 500;

    public Task<ImportResult> ImportEntries(
        int userId,
        int accountId,
        IEnumerable<CurrencyEntryImport> entries,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts = null,
        Func<int, int, int, Task>? onProgress = null) =>
        Import(userId, accountId, currencyId: null, entries, cancellationToken, onConflicts, onProgress);

    public Task<ImportResult> ImportOwnerEntries(
        int userId,
        int currencyId,
        IEnumerable<CurrencyEntryImport> entries,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts = null,
        Func<int, int, int, Task>? onProgress = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        return Import(userId, accountId: null, currencyId, entries, cancellationToken, onConflicts, onProgress);
    }

    // One import flow for both scopes. A concrete account id is the legacy account import; without one the
    // owner's pool of the selected currency is imported and nothing depends on a running value.
    private async Task<ImportResult> Import(
        int userId,
        int? accountId,
        int? currencyId,
        IEnumerable<CurrencyEntryImport> entries,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts,
        Func<int, int, int, Task>? onProgress)
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
                ? await currencyAccountRepository.Get(linkedAccountId, cancellationToken)
                : await currencyAccountRepository.Get(linkedAccountId);
            importAccountValidator.EnsureOwnership(account, userId);
        }

        var minDay = entryList.Min(x => x.PostingDate).Date;
        var maxDay = entryList.Max(x => x.PostingDate).Date;

        int imported = 0;
        int failed = 0;
        int processed = 0;
        var errors = new List<string>();
        var conflicts = new List<ImportConflict>();
        CurrencyAccountEntry? oldestInserted = null;

        var existingEntries = accountId is int existingAccountId
            ? cancellationToken.CanBeCanceled
                ? await currencyAccountEntryRepository
                    .Get(existingAccountId, minDay.AddDays(-1), maxDay.AddDays(1), cancellationToken)
                    .ToListAsync(cancellationToken)
                : await currencyAccountEntryRepository
                    .Get(existingAccountId, minDay.AddDays(-1), maxDay.AddDays(1))
                    .ToListAsync()
            // Owner rows of this currency from the first through the last imported UTC day, whatever their link.
            : await currencyAccountEntryRepository
                .GetByOwner(userId, currencyId!.Value, DateTime.SpecifyKind(minDay, DateTimeKind.Utc),
                    DateTime.SpecifyKind(maxDay.AddDays(1).AddTicks(-1), DateTimeKind.Utc), cancellationToken)
                .ToListAsync(cancellationToken);
        var importsByDay = entryList
            .GroupBy(x => x.PostingDate.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
        var existingByDay = existingEntries
            .GroupBy(e => e.PostingDate.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
        var transactionRuleApplication = transactionRuleService is null
            ? null
            : await transactionRuleService.LoadApplicationAsync(userId, cancellationToken);

        try
        {
            for (var day = maxDay; day >= minDay; day = day.AddDays(-1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!importsByDay.TryGetValue(day, out var importsThisDay)) continue;
                var existingThisDay = existingByDay.GetValueOrDefault(day) ?? [];

                var exactMatches = ImportConflictDetector.GetExactMatches(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();
                var importsOnly = ImportConflictDetector.GetImportsMissingFromExisting(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();

                // Only a real overlap needs review; unrelated rows already on the day never block an import.
                if (exactMatches.Count != 0)
                {
                    // Existing-only rows may come from unrelated statements, so they are reviewed in a legacy account import only.
                    var existingOnly = accountId is null
                        ? []
                        : ImportConflictDetector.GetExistingMissingFromImports(existingThisDay, importsThisDay, ExistingKey, ImportKey).ToList();
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

                var entriesToInsert = new List<CurrencyAccountEntry>(importsThisDay.Count);
                foreach (var import in importsThisDay)
                {
                    try
                    {
                        if (import.PostingDate.Kind != DateTimeKind.Utc)
                            throw new Exception($"Date kind of this entry posting date: {import.PostingDate}, value change: {import.ValueChange} is not UTC - {import.PostingDate.Kind}");

                        var newEntry = NewEntry(userId, accountId, currencyId, import);
                        transactionRuleApplication?.ApplyTo(newEntry);
                        entriesToInsert.Add(newEntry);
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

                foreach (var batch in entriesToInsert.Chunk(_persistenceBatchSize))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batchList = batch.ToList();
                    try
                    {
                        var added = cancellationToken.CanBeCanceled
                            ? await currencyAccountEntryRepository.Add(batchList, recalculate: false, cancellationToken)
                            : await currencyAccountEntryRepository.Add(batchList, recalculate: false);
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
                        logger.LogDebug(ex, "Currency account import batch cancelled.");
                        throw;
                    }
                    catch (OperationCanceledException ex)
                    {
                        failed += batchList.Count;
                        errors.AddRange(batchList.Select(entry => $"Cancellation while importing entry with date {entry.PostingDate}."));
                        logger.LogDebug(ex, "Currency account import batch cancelled or timed out.");
                    }
                    catch (Exception ex)
                    {
                        failed += batchList.Count;
                        errors.AddRange(batchList.Select(_ => ex.Message));
                    }
                }

                processed += importsThisDay.Count;
                if (onProgress is not null)
                    await onProgress(processed, imported, failed);
            }

            if (accountId is int recalculatedAccountId && oldestInserted is not null)
            {
                try
                {
                    if (cancellationToken.CanBeCanceled)
                        await currencyAccountEntryRepository.RecalculateValues(recalculatedAccountId, oldestInserted.EntryId, cancellationToken);
                    else
                        await currencyAccountEntryRepository.RecalculateValues(recalculatedAccountId, oldestInserted.EntryId);
                }
                catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Currency account import recalculation cancelled.");
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    await RecalculateCommittedEntries(recalculatedAccountId, oldestInserted);
                    failed++;
                    errors.Add("Recalculation cancelled or timed out.");
                    logger.LogDebug(ex, "Currency account import recalculation cancelled or timed out; marking it failed.");
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

    public async Task<bool> ApplyOwnerConflict(int userId, int currencyId, CurrencyEntryImport entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        cancellationToken.ThrowIfCancellationRequested();

        if (entry.PostingDate.Kind != DateTimeKind.Utc)
        {
            logger.LogError("Could not add a resolved currency import entry: its posting date is not UTC.");
            return false;
        }

        // An approved addition counts against the plan like an initial admission.
        await importAccountValidator.EnsureWithinPlanLimit(userId, 1);

        var newEntry = NewEntry(userId, accountId: null, currencyId, entry);
        if (transactionRuleService is not null)
            (await transactionRuleService.LoadApplicationAsync(userId, cancellationToken)).ApplyTo(newEntry);

        try
        {
            var added = await currencyAccountEntryRepository.Add(newEntry, recalculate: false, cancellationToken);
            if (!added) logger.LogError("Could not add a resolved currency import entry.");
            return added;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Applying resolved currency import conflict cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying resolved currency import conflict.");
            return false;
        }
    }

    // A linked entry is stamped by the repository from its account and carries the running value; an owner entry
    // carries the validated owner and currency and keeps no running value.
    private static CurrencyAccountEntry NewEntry(int userId, int? accountId, int? currencyId, CurrencyEntryImport import) =>
        new(accountId, 0, ImportDateNormalizer.ToSecond(import.PostingDate), accountId is null ? 0 : import.ValueChange, import.ValueChange)
        {
            UserId = accountId is null ? userId : null,
            CurrencyId = accountId is null ? currencyId : null,
            Description = import.Description ?? string.Empty,
            ContractorDetails = import.ContractorDetails,
            Labels = []
        };

    private Task RecalculateCommittedEntries(int accountId, CurrencyAccountEntry? oldestInserted) =>
        oldestInserted is null
            ? Task.CompletedTask
            : currencyAccountEntryRepository.RecalculateValues(accountId, oldestInserted.EntryId, CancellationToken.None);

    public async Task ApplyResolvedConflicts(
        IEnumerable<ResolvedImportConflict> resolvedConflicts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolvedConflicts);
        cancellationToken.ThrowIfCancellationRequested();

        var accountsById = new Dictionary<int, CurrencyAccount?>();
        var ruleApplicationsByUserId = new Dictionary<int, TransactionRuleApplication>();

        foreach (var resolvedConflict in resolvedConflicts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!resolvedConflict.LeaveExisting && resolvedConflict.ExistingId is long existingId)
                    if (cancellationToken.CanBeCanceled)
                        await currencyAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId, cancellationToken);
                    else
                        await currencyAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId);

                if (resolvedConflict.AddImported && resolvedConflict.ImportData is not null)
                {
                    var resolvedEntry = resolvedConflict.ToEntry();
                    if (transactionRuleService is not null)
                    {
                        if (!accountsById.TryGetValue(resolvedConflict.AccountId, out var resolvedAccount))
                        {
                            resolvedAccount = await currencyAccountRepository.Get(resolvedConflict.AccountId, cancellationToken);
                            accountsById.Add(resolvedConflict.AccountId, resolvedAccount);
                        }

                        if (resolvedAccount is not null)
                        {
                            if (!ruleApplicationsByUserId.TryGetValue(resolvedAccount.UserId, out var ruleApplication))
                            {
                                ruleApplication = await transactionRuleService.LoadApplicationAsync(resolvedAccount.UserId, cancellationToken);
                                ruleApplicationsByUserId.Add(resolvedAccount.UserId, ruleApplication);
                            }

                            ruleApplication.ApplyTo(resolvedEntry);
                        }
                    }

                    var added = cancellationToken.CanBeCanceled
                        ? await currencyAccountEntryRepository.Add(resolvedEntry, recalculate: true, cancellationToken)
                        : await currencyAccountEntryRepository.Add(resolvedEntry);
                    if (!added) logger.LogError("Could not add a resolved currency import entry.");
                }
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Applying resolved currency import conflict cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying resolved currency import conflict.");
            }
        }
    }

    // Currency entries are compared on posting date (second precision) and value change.
    private static (DateTime Date, decimal ValueChange) ImportKey(CurrencyEntryImport import) =>
        (ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange);

    private static (DateTime Date, decimal ValueChange) ExistingKey(CurrencyAccountEntry entry) =>
        (ImportDateNormalizer.ToSecond(entry.PostingDate), entry.ValueChange);
}