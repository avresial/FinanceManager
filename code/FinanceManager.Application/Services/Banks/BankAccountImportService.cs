using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Banks;

public class BankAccountImportService(IBankAccountRepository<BankAccount> bankAccountRepository,
    IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository,
    IUserPlanVerifier userPlanVerifier, ILogger<BankAccountImportService> logger) : IBankAccountImportService
{
    public async Task<ImportResult> ImportEntries(int userId, int accountId, IEnumerable<BankEntryImport> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(accountId, 0, 0, [], []);

        if (!await userPlanVerifier.CanAddMoreEntries(userId, entryList.Count))
            throw new InvalidOperationException("Plan does not allow importing this many entries.");

        var account = await bankAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId) throw new InvalidOperationException("Account not found or access denied.");

        var minDay = entryList.Min(x => x.PostingDate).Date;
        var maxDay = entryList.Max(x => x.PostingDate).Date;

        int imported = 0;
        int failed = 0;
        var errors = new List<string>();
        var conflicts = new List<ImportConflict>();

        var existingAll = await bankAccountEntryRepository.Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1)).ToListAsync();
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
                conflicts.AddRange(exactMatches.Select(x => new ImportConflict(accountId, x.Import, x.Existing, "Exact match")));
                conflicts.AddRange(importsOnlyConflicts);
                conflicts.AddRange(existingOnlyConflicts);

                continue;
            }

            foreach (var imp in importsThisDay)
            {
                try
                {
                    if (imp.PostingDate.Kind != DateTimeKind.Utc)
                        throw new Exception($"Date kind of this entry posting date: {imp.PostingDate}, value change: {imp.ValueChange} is not UTC - {imp.PostingDate.Kind}");

                    BankAccountEntry newEntry = new(accountId, 0, imp.PostingDate, imp.ValueChange, imp.ValueChange)
                    {
                        Description = string.Empty,
                        Labels = []
                    };

                    if (await bankAccountEntryRepository.Add(newEntry, day == minDay))
                    {
                        imported++;
                        existingAll.Add(newEntry);
                    }
                    else
                    {
                        failed++;
                        errors.Add($"Failed to import entry with date {imp.PostingDate}.");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add(ex.Message);
                }
            }
        }

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
                    await bankAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId);

                if (resolvedConflict.AddImported && resolvedConflict.ImportData is not null)
                {
                    var importData = resolvedConflict.ImportData;
                    await bankAccountEntryRepository.Add(resolvedConflict.ToEntry());
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying resolved conflict for account {AccountId}", resolvedConflict.AccountId);
            }
        }
    }

    private static IEnumerable<(BankEntryImport Import, BankAccountEntry Existing)> GetExactMatches(List<BankEntryImport> imports, List<BankAccountEntry> existing)
    {
        foreach (var import in imports.GroupBy(x => (Date: x.PostingDate, ValuceChange: x.ValueChange)))
        {
            var sameExisting = existing.Where(e => e.PostingDate == import.Key.Date && e.ValueChange == import.Key.ValuceChange).ToList();

            if (sameExisting.Count != 0 && import.Any())
            {
                List<int> counts = [sameExisting.Count, import.Count()];
                for (int i = 0; i < counts.Min(); i++)
                    yield return (import.First(), sameExisting.First());
            }
        }
    }

    private static IEnumerable<ImportConflict> GetImportsWhichAreMissingFromExisting(int accountId, IEnumerable<BankEntryImport> imports, IEnumerable<BankAccountEntry> existing)
    {
        foreach (var import in imports.GroupBy(x => (Date: x.PostingDate, ValuceChange: x.ValueChange)))
        {
            var sameExistingCount = existing.Count(e => e.PostingDate == import.Key.Date && e.ValueChange == import.Key.ValuceChange);

            if (import.Count() > sameExistingCount && import.Count() != 0)
            {
                for (int i = 0; i < import.Count() - sameExistingCount; i++)
                    yield return new ImportConflict(accountId, import.First(), null, "Import not found in existing");
            }
        }
    }

    private static IEnumerable<ImportConflict> GetExistingWhichAreMissingFromImports(int accountId, IEnumerable<BankAccountEntry> existing, IEnumerable<BankEntryImport> imports)
    {
        foreach (var existingItem in existing.GroupBy(x => (Date: x.PostingDate, ValuceChange: x.ValueChange)))
        {
            var sameImportsCount = imports.Count(e => e.PostingDate == existingItem.Key.Date && e.ValueChange == existingItem.Key.ValuceChange);

            if (existingItem.Count() > sameImportsCount && existingItem.Count() != 0)
            {
                for (int i = 0; i < existingItem.Count() - sameImportsCount; i++)
                    yield return new ImportConflict(accountId, null, existingItem.First(), "Existing not fount in import");
            }
        }
    }
}