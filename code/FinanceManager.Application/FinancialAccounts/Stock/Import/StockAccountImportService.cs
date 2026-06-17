using FinanceManager.Application.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Imports;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Stock.Import;

public class StockAccountImportService(
    IAccountRepository<StockAccount> stockAccountRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository,
    IStockDetailsRepository stockDetailsRepository,
    ImportAccountValidator importAccountValidator,
    ILogger<StockAccountImportService> logger) : IStockAccountImportService
{
    public async Task<StockImportResult> ImportEntries(int userId, int accountId, IEnumerable<StockEntryImport> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryList = entries.OrderBy(e => e.PostingDate).ToList();
        if (entryList.Count == 0)
            return new(accountId, 0, 0, [], []);

        await importAccountValidator.EnsureWithinPlanLimit(userId, entryList.Count);

        var account = await stockAccountRepository.Get(accountId);
        importAccountValidator.EnsureOwnership(account, userId);

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

            var exactMatches = ImportConflictDetector.GetExactMatches(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();
            var importsOnly = ImportConflictDetector.GetImportsMissingFromExisting(importsThisDay, existingThisDay, ImportKey, ExistingKey).ToList();
            var existingOnly = ImportConflictDetector.GetExistingMissingFromImports(existingThisDay, importsThisDay, ExistingKey, ImportKey).ToList();

            if (exactMatches.Count != 0 || existingOnly.Count != 0)
            {
                conflicts.AddRange(exactMatches.Select(x => new StockImportConflict(accountId, x.Import, x.Existing, "Exact match")));
                conflicts.AddRange(importsOnly.Select(x => new StockImportConflict(accountId, x, null, "Import not found in existing")));
                conflicts.AddRange(existingOnly.Select(x => new StockImportConflict(accountId, null, x, "Existing not found in import")));
                continue;
            }

            foreach (var import in importsThisDay)
            {
                try
                {
                    if (import.PostingDate.Kind != DateTimeKind.Utc)
                        throw new Exception($"Date kind of this entry posting date: {import.PostingDate}, value change: {import.ValueChange} is not UTC - {import.PostingDate.Kind}");

                    var stockDetails = await stockDetailsRepository.Get(import.Isin);
                    var ticker = stockDetails?.Ticker ?? string.Empty;

                    var newEntry = new StockAccountEntry(accountId, 0, ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange, import.ValueChange, import.Isin, InvestmentType.Stock)
                    {
                        Ticker = ticker
                    };
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

        foreach (var tickerGroup in entriesToRecalc.GroupBy(e => e.Isin, StringComparer.OrdinalIgnoreCase))
        {
            var earliest = tickerGroup.OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId).FirstOrDefault();
            if (earliest is null) continue;

            await stockAccountEntryRepository.RecalculateValues(accountId, earliest.EntryId);
        }
    }

    // Stock entries are compared on posting date (second precision), value change and ISIN.
    // ISIN is upper-cased so the key comparison is case-insensitive, matching the legacy
    // OrdinalIgnoreCase comparison.
    private static (DateTime Date, decimal ValueChange, string Isin) ImportKey(StockEntryImport import) =>
        (ImportDateNormalizer.ToSecond(import.PostingDate), import.ValueChange, import.Isin.ToUpperInvariant());

    private static (DateTime Date, decimal ValueChange, string Isin) ExistingKey(StockAccountEntry entry) =>
        (ImportDateNormalizer.ToSecond(entry.PostingDate), entry.ValueChange, entry.Isin.ToUpperInvariant());
}