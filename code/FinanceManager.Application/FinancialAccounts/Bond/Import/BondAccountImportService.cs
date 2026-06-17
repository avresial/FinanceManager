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
    public async Task<BondImportResult> ImportEntries(int userId, int accountId, IEnumerable<BondEntryImport> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(accountId, 0, 0, [], []);

        await importAccountValidator.EnsureWithinPlanLimit(userId, entryList.Count);

        var account = await bondAccountRepository.Get(accountId);
        importAccountValidator.EnsureOwnership(account, userId);

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

            foreach (var import in importsThisDay)
            {
                try
                {
                    if (import.PostingDate.Kind != DateTimeKind.Utc)
                        throw new Exception($"Date kind of this entry posting date: {import.PostingDate}, value change: {import.ValueChange} is not UTC - {import.PostingDate.Kind}");

                    var newEntry = new BondAccountEntry(accountId, 0, ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange, import.ValueChange, import.BondDetailsId);
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

    // Bond entries are compared on posting date (second precision), value change and bond details.
    private static (DateTime Date, decimal ValueChange, int BondDetailsId) ImportKey(BondEntryImport import) =>
        (ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange, import.BondDetailsId);

    private static (DateTime Date, decimal ValueChange, int BondDetailsId) ExistingKey(BondAccountEntry entry) =>
        (ImportDateNormalizer.ToSecond(entry.PostingDate), entry.ValueChange, entry.BondDetailsId);
}