using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Bond.Import;

public class BondAccountImportService(
    IAccountRepository<BondAccount> bondAccountRepository,
    IBondAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository,
    IUserPlanVerifier userPlanVerifier,
    ILogger<BondAccountImportService> logger) : IBondAccountImportService
{
    public async Task<BondImportResult> ImportEntries(int userId, int accountId, IEnumerable<BondEntryImport> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(accountId, 0, 0, [], []);

        if (!await userPlanVerifier.CanAddMoreEntries(userId, entryList.Count))
            throw new InvalidOperationException("Plan does not allow importing this many entries.");

        var account = await bondAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId)
            throw new InvalidOperationException("Account not found or access denied.");

        var minDay = entryList.Min(x => x.PostingDate).Date;
        var maxDay = entryList.Max(x => x.PostingDate).Date;

        int imported = 0;
        int failed = 0;
        var errors = new List<string>();
        var conflicts = new List<BondImportConflict>();

        var existingAll = await bondAccountEntryRepository.Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1)).ToListAsync();
        for (var day = maxDay; day >= minDay; day = day.AddDays(-1))
        {
            var importsThisDay = entryList.Where(x => x.PostingDate.Date == day).ToList();
            var existingThisDay = existingAll.Where(e => e.PostingDate.Date == day).ToList();

            if (importsThisDay.Count == 0)
                continue;

            var exactMatches = GetExactMatches(importsThisDay, existingThisDay).ToList();
            var importsOnlyConflicts = GetImportsWhichAreMissingFromExisting(accountId, importsThisDay, existingThisDay).ToList();
            var existingOnlyConflicts = GetExistingWhichAreMissingFromImports(accountId, existingThisDay, importsThisDay).ToList();

            if (exactMatches.Count != 0 || existingOnlyConflicts.Count != 0)
            {
                conflicts.AddRange(exactMatches.Select(x => new BondImportConflict(accountId, x.Import, x.Existing, "Exact match")));
                conflicts.AddRange(importsOnlyConflicts);
                conflicts.AddRange(existingOnlyConflicts);
                continue;
            }

            foreach (var import in importsThisDay)
            {
                try
                {
                    if (import.PostingDate.Kind != DateTimeKind.Utc)
                        throw new Exception($"Date kind of this entry posting date: {import.PostingDate}, value change: {import.ValueChange} is not UTC - {import.PostingDate.Kind}");

                    var newEntry = new BondAccountEntry(accountId, 0, ToSecond(import.PostingDate), import.ValueChange, import.ValueChange, import.BondDetailsId);
                    if (await bondAccountEntryRepository.Add(newEntry, recalculate: false))
                    {
                        imported++;
                        existingAll.Add(newEntry);
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
        }

        if (imported > 0)
            await RecalculateBonds(accountId, minDay, maxDay);

        return new(accountId, imported, failed, errors, conflicts);
    }

    public async Task ApplyResolvedConflicts(IEnumerable<ResolvedBondImportConflict> resolvedConflicts)
    {
        ArgumentNullException.ThrowIfNull(resolvedConflicts);

        foreach (var resolvedConflict in resolvedConflicts)
        {
            try
            {
                if (!resolvedConflict.LeaveExisting && resolvedConflict.ExistingId is int existingId)
                    await bondAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId);

                if (resolvedConflict.AddImported && resolvedConflict.ImportData is not null)
                {
                    var entry = resolvedConflict.ToEntry();
                    await bondAccountEntryRepository.Add(entry, recalculate: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying resolved conflict for account {AccountId}", resolvedConflict.AccountId);
            }
        }
    }

    private async Task RecalculateBonds(int accountId, DateTime minDay, DateTime maxDay)
    {
        var entriesToRecalc = await bondAccountEntryRepository.Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1)).ToListAsync();

        foreach (var bondGroup in entriesToRecalc.GroupBy(e => e.BondDetailsId))
        {
            var earliest = bondGroup.OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId).FirstOrDefault();
            if (earliest is null)
                continue;

            await bondAccountEntryRepository.RecalculateValues(accountId, earliest.EntryId);
        }
    }

    // Posting dates are stored and compared at second precision across the app.
    // Truncating here keeps fractional-second legacy DB entries comparable with
    // freshly imported / exported CSV rows, which carry second precision.
    private static DateTime ToSecond(DateTime d) =>
        new(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Kind);

    private static IEnumerable<(BondEntryImport Import, BondAccountEntry Existing)> GetExactMatches(List<BondEntryImport> imports, List<BondAccountEntry> existing)
    {
        foreach (var import in imports.GroupBy(x => (Date: ToSecond(x.PostingDate), ValueChange: x.ValueChange, BondDetailsId: x.BondDetailsId)))
        {
            var sameExisting = existing
                .Where(e => ToSecond(e.PostingDate) == import.Key.Date && e.ValueChange == import.Key.ValueChange && e.BondDetailsId == import.Key.BondDetailsId)
                .ToList();

            if (sameExisting.Count != 0 && import.Any())
            {
                List<int> counts = [sameExisting.Count, import.Count()];
                for (int i = 0; i < counts.Min(); i++)
                    yield return (import.ToArray()[i], sameExisting.ToArray()[i]);
            }
        }
    }

    private static IEnumerable<BondImportConflict> GetImportsWhichAreMissingFromExisting(int accountId, IEnumerable<BondEntryImport> imports, IEnumerable<BondAccountEntry> existing)
    {
        foreach (var import in imports.GroupBy(x => (Date: ToSecond(x.PostingDate), ValueChange: x.ValueChange, BondDetailsId: x.BondDetailsId)))
        {
            var importItemList = import.ToList();
            var sameExistingCount = existing.Count(e =>
                ToSecond(e.PostingDate) == import.Key.Date && e.ValueChange == import.Key.ValueChange && e.BondDetailsId == import.Key.BondDetailsId);

            if (importItemList.Count > sameExistingCount && importItemList.Count != 0)
            {
                for (int i = 0; i < importItemList.Count - sameExistingCount; i++)
                    yield return new BondImportConflict(accountId, importItemList.First(), null, "Import not found in existing");
            }
        }
    }

    private static IEnumerable<BondImportConflict> GetExistingWhichAreMissingFromImports(int accountId, IEnumerable<BondAccountEntry> existing, IEnumerable<BondEntryImport> imports)
    {
        foreach (var existingItem in existing.GroupBy(x => (Date: ToSecond(x.PostingDate), ValueChange: x.ValueChange, BondDetailsId: x.BondDetailsId)))
        {
            var existingItemList = existingItem.ToList();
            var sameImportsCount = imports.Count(e =>
                ToSecond(e.PostingDate) == existingItem.Key.Date && e.ValueChange == existingItem.Key.ValueChange && e.BondDetailsId == existingItem.Key.BondDetailsId);

            if (existingItemList.Count <= sameImportsCount || existingItemList.Count == 0)
                continue;

            for (int i = sameImportsCount; i < existingItemList.Count; i++)
                yield return new BondImportConflict(accountId, null, existingItemList[i], "Existing not found in import");
        }
    }
}