using FinanceManager.Application.TransactionRules;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Cash.Imports;
using FinanceManager.Domain.Transactions.Import;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Transactions.Import;

public class CashImportService(ITransactionEntryRepository<CashEntry> currencyAccountEntryRepository,
    ImportEntryLimitValidator importEntryLimitValidator, ILogger<CashImportService> logger,
    ITransactionRuleService? transactionRuleService = null) : ICashImportService
{
    // Keep each persistence operation bounded so large imports retain partial-failure semantics and
    // do not create an unbounded EF change tracker or provider command batch.
    private const int _persistenceBatchSize = 500;

    public async Task<ImportResult> ImportOwnerEntries(
        int userId,
        int currencyId,
        IEnumerable<CurrencyEntryImport> entries,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts = null,
        Func<int, int, int, Task>? onProgress = null)
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
        int processed = 0;
        var errors = new List<string>();
        var conflicts = new List<ImportConflict>();

        var existingEntries = await currencyAccountEntryRepository
                .GetByOwner(userId, currencyId, DateTime.SpecifyKind(minDay, DateTimeKind.Utc),
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
                var dailyConflicts = new List<ImportConflict>();
                dailyConflicts.AddRange(exactMatches.Select(x => new ImportConflict(x.Import, x.Existing, "Exact match")));
                dailyConflicts.AddRange(importsOnly.Select(x => new ImportConflict(x, null, "Import not found in existing")));

                conflicts.AddRange(dailyConflicts);

                if (onConflicts is not null)
                    await onConflicts(dailyConflicts);

                processed += importsThisDay.Count;
                if (onProgress is not null)
                    await onProgress(processed, imported, failed);

                continue;
            }

            var entriesToInsert = new List<CashEntry>(importsThisDay.Count);
            foreach (var import in importsThisDay)
            {
                try
                {
                    if (import.PostingDate.Kind != DateTimeKind.Utc)
                        throw new Exception($"Date kind of this entry posting date: {import.PostingDate}, value change: {import.ValueChange} is not UTC - {import.PostingDate.Kind}");

                    var newEntry = NewEntry(userId, currencyId, import);
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
                    var added = await currencyAccountEntryRepository.Add(batchList, cancellationToken);
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

        return new(imported, failed, errors, conflicts);
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
        await importEntryLimitValidator.EnsureWithinPlanLimit(userId, 1);

        var newEntry = NewEntry(userId, currencyId, entry);
        if (transactionRuleService is not null)
            (await transactionRuleService.LoadApplicationAsync(userId, cancellationToken)).ApplyTo(newEntry);

        try
        {
            var added = await currencyAccountEntryRepository.Add(newEntry, cancellationToken);
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

    private static CashEntry NewEntry(int userId, int currencyId, CurrencyEntryImport import) =>
        new(0, ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange)
        {
            UserId = userId,
            CurrencyId = currencyId,
            Description = import.Description ?? string.Empty,
            ContractorDetails = import.ContractorDetails,
            Labels = []
        };

    // Currency entries are compared on posting date (second precision) and value change.
    private static (DateTime Date, decimal ValueChange) ImportKey(CurrencyEntryImport import) =>
        (ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange);

    private static (DateTime Date, decimal ValueChange) ExistingKey(CashEntry entry) =>
        (ImportDateNormalizer.ToSecond(entry.PostingDate), entry.ValueChange);
}