using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Stocks;

public class StockAccountImportService(
    IAccountRepository<StockAccount> stockAccountRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository,
    IUserPlanVerifier userPlanVerifier,
    ILogger<StockAccountImportService> logger) : IStockAccountImportService
{
    public async Task<StockImportResult> ImportEntries(int userId, int accountId, IEnumerable<StockEntryImport> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(accountId, 0, 0, [], []);

        if (!await userPlanVerifier.CanAddMoreEntries(userId, entryList.Count))
            throw new InvalidOperationException("Plan does not allow importing this many entries.");

        var account = await stockAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId)
            throw new InvalidOperationException("Account not found or access denied.");

        var minDay = entryList.Min(x => x.PostingDate).Date;
        var maxDay = entryList.Max(x => x.PostingDate).Date;

        int imported = 0;
        int failed = 0;
        var errors = new List<string>();
        var conflicts = new List<StockImportConflict>();

        var existingAll = await stockAccountEntryRepository.Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1)).ToListAsync();
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
                conflicts.AddRange(exactMatches.Select(x => new StockImportConflict(accountId, x.Import, x.Existing, "Exact match")));
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

                    var newEntry = new StockAccountEntry(accountId, 0, import.PostingDate, import.ValueChange, import.ValueChange, import.Ticker, InvestmentType.Stock);
                    if (await stockAccountEntryRepository.Add(newEntry, recalculate: false))
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
            await RecalculateTickers(accountId, minDay, maxDay);

        return new(accountId, imported, failed, errors, conflicts);
    }

    public async Task ApplyResolvedConflicts(IEnumerable<ResolvedStockImportConflict> resolvedConflicts)
    {
        ArgumentNullException.ThrowIfNull(resolvedConflicts);

        foreach (var resolvedConflict in resolvedConflicts)
        {
            try
            {
                if (!resolvedConflict.LeaveExisting && resolvedConflict.ExistingId is int existingId)
                    await stockAccountEntryRepository.Delete(resolvedConflict.AccountId, existingId);

                if (resolvedConflict.AddImported && resolvedConflict.ImportData is not null)
                {
                    var importData = resolvedConflict.ImportData;
                    var entry = resolvedConflict.ToEntry();
                    await stockAccountEntryRepository.Add(entry, recalculate: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying resolved conflict for account {AccountId}", resolvedConflict.AccountId);
            }
        }
    }

    private async Task RecalculateTickers(int accountId, DateTime minDay, DateTime maxDay)
    {
        var entriesToRecalc = await stockAccountEntryRepository.Get(accountId, minDay.AddDays(-1), maxDay.AddDays(1))
            .ToListAsync();

        foreach (var tickerGroup in entriesToRecalc.GroupBy(e => e.Ticker, StringComparer.OrdinalIgnoreCase))
        {
            var earliest = tickerGroup.OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId).FirstOrDefault();
            if (earliest is null) continue;

            await stockAccountEntryRepository.RecalculateValues(accountId, earliest.EntryId);
        }
    }

    private static IEnumerable<(StockEntryImport Import, StockAccountEntry Existing)> GetExactMatches(List<StockEntryImport> imports, List<StockAccountEntry> existing)
    {
        foreach (var import in imports.GroupBy(x => (Date: x.PostingDate, ValueChange: x.ValueChange, Ticker: x.Ticker)))
        {
            var sameExisting = existing
                .Where(e => e.PostingDate == import.Key.Date && e.ValueChange == import.Key.ValueChange &&
                            string.Equals(e.Ticker, import.Key.Ticker, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sameExisting.Count != 0 && import.Any())
            {
                List<int> counts = [sameExisting.Count, import.Count()];
                for (int i = 0; i < counts.Min(); i++)
                    yield return (import.ToArray()[i], sameExisting.ToArray()[i]);
            }
        }
    }

    private static IEnumerable<StockImportConflict> GetImportsWhichAreMissingFromExisting(int accountId, IEnumerable<StockEntryImport> imports, IEnumerable<StockAccountEntry> existing)
    {
        foreach (var import in imports.GroupBy(x => (Date: x.PostingDate, ValueChange: x.ValueChange, Ticker: x.Ticker)))
        {
            var importItemList = import.ToList();
            var sameExistingCount = existing.Count(e => e.PostingDate == import.Key.Date && e.ValueChange == import.Key.ValueChange &&
                string.Equals(e.Ticker, import.Key.Ticker, StringComparison.OrdinalIgnoreCase));

            if (importItemList.Count > sameExistingCount && importItemList.Count != 0)
            {
                for (int i = 0; i < importItemList.Count - sameExistingCount; i++)
                    yield return new StockImportConflict(accountId, importItemList.First(), null, "Import not found in existing");
            }
        }
    }

    private static IEnumerable<StockImportConflict> GetExistingWhichAreMissingFromImports(int accountId, IEnumerable<StockAccountEntry> existing, IEnumerable<StockEntryImport> imports)
    {
        foreach (var existingItem in existing.GroupBy(x => (Date: x.PostingDate, ValueChange: x.ValueChange, Ticker: x.Ticker)))
        {
            var existingItemList = existingItem.ToList();
            var sameImportsCount = imports.Count(e => e.PostingDate == existingItem.Key.Date && e.ValueChange == existingItem.Key.ValueChange &&
                string.Equals(e.Ticker, existingItem.Key.Ticker, StringComparison.OrdinalIgnoreCase));

            if (existingItemList.Count <= sameImportsCount || existingItemList.Count == 0)
                continue;

            for (int i = sameImportsCount; i < existingItemList.Count; i++)
                yield return new StockImportConflict(accountId, null, existingItemList[i], "Existing not found in import");
        }
    }
}