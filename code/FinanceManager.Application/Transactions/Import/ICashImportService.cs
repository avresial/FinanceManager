using FinanceManager.Domain.Transactions.Cash.Imports;
using FinanceManager.Domain.Transactions.Import;

namespace FinanceManager.Application.Transactions.Import;

public interface ICashImportService
{
    /// <summary>
    /// Imports into the owner's pool of one currency. Every row of that currency the owner has, linked or not, is
    /// compared; no account is involved and no running value is recalculated. Exact matches are treated as already
    /// present and only the incoming surplus is reported as a conflict.
    /// </summary>
    Task<ImportResult> ImportOwnerEntries(
        int userId,
        int currencyId,
        IEnumerable<CurrencyEntryImport> entries,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts = null,
        Func<int, int, int, Task>? onProgress = null);

    /// <summary>Adds one incoming row the owner accepted during review. Returns false when the write failed.</summary>
    Task<bool> ApplyOwnerConflict(int userId, int currencyId, CurrencyEntryImport entry, CancellationToken cancellationToken);
}