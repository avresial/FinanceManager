using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Import;

public class CurrencyAccountImportService(ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyAccountEntryRepository,
    IUserPlanVerifier userPlanVerifier, ILogger<CurrencyAccountImportService> logger) : ICurrencyAccountImportService
{
    public async Task<ImportResult> ImportEntries(
        int userId,
        int accountId,
        IEnumerable<CurrencyEntryImport> entries,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts = null,
        Func<int, int, int, Task>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(accountId, 0, 0, [], []);

        if (!await userPlanVerifier.CanAddMoreEntries(userId, entryList.Count))
            throw new InvalidOperationException("Plan does not allow importing this many entries.");

        var account = await currencyAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId) throw new InvalidOperationException("Account not found or access denied.");

        var minDay = entryList.Min(x => x.PostingDate).Date;
        var maxDay = entryList.Max(x => x.PostingDate).Date;

        int imported = 0;
        int failed = 0;
        int processed = 0;
        var errors = new List<string>();
        var conflicts = new List<ImportConflict>();
        CurrencyAccountEntry? oldestInserted = null;

        var existingAll = await currencyAccountEntryRepository.Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1)).ToListAsync();
        for (var day = maxDay; day >= minDay; day = day.AddDays(-1))
        {
            var importsThisDay = entryList.Where(x => x.PostingDate.Date == day).ToList();
            var existingThisDay = existingAll.Where(e => e.PostingDate.Date == day).ToList();

            if (importsThisDay.Count == 0) continue;

            var exactMatches = GetExactMatches(importsThisDay, existingThisDay).ToList();
            var importsOnlyConflicts = GetImportsWhichAreMissingFromExisting(accountId, importsThisDay, existingThisDay).ToList();
            var existingOnlyConflicts = GetExistingWhichAreMissingFromImports(accountId, existingThisDay, importsThisDay).ToList();

            if (exactMatches.Count != 0 || existingOnlyConflicts.Count != 0)
            {
                var dailyConflicts = new List<ImportConflict>();
                dailyConflicts.AddRange(exactMatches.Select(x => new ImportConflict(accountId, x.Import, x.Existing, "Exact match")));
                dailyConflicts.AddRange(importsOnlyConflicts);
                dailyConflicts.AddRange(existingOnlyConflicts);

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

                    CurrencyAccountEntry newEntry = new(accountId, 0, ToSecond(import.PostingDate), import.ValueChange, import.ValueChange)
                    {
                        Description = import.Description ?? string.Empty,
                        ContractorDetails = import.ContractorDetails,
                        Labels = []
                    };

                    if (await currencyAccountEntryRepository.Add(newEntry, recalculate: false))
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
            await currencyAccountEntryRepository.RecalculateValues(accountId, oldestInserted.EntryId);

        return new(accountId, imported, failed, errors, conflicts);
    }

    public async Task ApplyResolvedConflicts(IEnumerable<ResolvedImportConflict> resolvedConflicts)
    {
        ArgumentNullException.ThrowIfNull(resolvedConflicts);

        foreach (var resolvedConflict in resolvedConflicts)
        {
            try
            {
                if (!resolvedConflict.LeaveExisting && resolvedConflict.ExistingId is int existingId)
                    await currencyAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId);

                if (resolvedConflict.AddImported && resolvedConflict.ImportData is not null)
                {
                    var importData = resolvedConflict.ImportData;
                    await currencyAccountEntryRepository.Add(resolvedConflict.ToEntry());
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying resolved conflict for account {AccountId}", resolvedConflict.AccountId);
            }
        }
    }

    // Posting dates are stored and compared at second precision across the app.
    // Truncating both sides keeps legacy fractional-second DB entries comparable
    // with second-precision CSV imports.
    private static DateTime ToSecond(DateTime d) =>
        new(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Kind);

    private static IEnumerable<(CurrencyEntryImport Import, CurrencyAccountEntry Existing)> GetExactMatches(List<CurrencyEntryImport> imports, List<CurrencyAccountEntry> existing)
    {
        foreach (var import in imports.GroupBy(x => (Date: ToSecond(x.PostingDate), ValuceChange: x.ValueChange)))
        {
            var sameExisting = existing.Where(e => ToSecond(e.PostingDate) == import.Key.Date && e.ValueChange == import.Key.ValuceChange).ToList();

            if (sameExisting.Count != 0 && import.Any())
            {
                List<int> counts = [sameExisting.Count, import.Count()];
                for (int i = 0; i < counts.Min(); i++)
                    yield return (import.ToArray()[i], sameExisting.ToArray()[i]);
            }
        }
    }

    private static IEnumerable<ImportConflict> GetImportsWhichAreMissingFromExisting(int accountId, IEnumerable<CurrencyEntryImport> imports, IEnumerable<CurrencyAccountEntry> existing)
    {
        foreach (var import in imports.GroupBy(x => (Date: ToSecond(x.PostingDate), ValuceChange: x.ValueChange)))
        {
            var importItemList = import.ToList();
            var sameExistingCount = existing.Count(e => ToSecond(e.PostingDate) == import.Key.Date && e.ValueChange == import.Key.ValuceChange);

            if (importItemList.Count > sameExistingCount && importItemList.Count != 0)
            {
                for (int i = 0; i < importItemList.Count - sameExistingCount; i++)
                    yield return new ImportConflict(accountId, importItemList.First(), null, "Import not found in existing");
            }
        }
    }

    private static IEnumerable<ImportConflict> GetExistingWhichAreMissingFromImports(int accountId, IEnumerable<CurrencyAccountEntry> existing, IEnumerable<CurrencyEntryImport> imports)
    {
        foreach (var existingItem in existing.GroupBy(x => (Date: ToSecond(x.PostingDate), ValuceChange: x.ValueChange)))
        {
            var existingItemList = existingItem.ToList();
            var sameImportsCount = imports.Count(e => ToSecond(e.PostingDate) == existingItem.Key.Date && e.ValueChange == existingItem.Key.ValuceChange);

            if (existingItemList.Count <= sameImportsCount || existingItemList.Count == 0)
                continue;

            for (int i = sameImportsCount; i < existingItemList.Count; i++)
                yield return new ImportConflict(accountId, null, existingItemList[i], "Existing not fount in import");
        }
    }
}