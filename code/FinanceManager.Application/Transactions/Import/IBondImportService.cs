using FinanceManager.Domain.Transactions.Bonds.Imports;

namespace FinanceManager.Application.Transactions.Import;

public interface IBondImportService
{
    /// <summary>
    /// Imports into the owner's pool; each row names its bond definition. Every row the owner has for a row's
    /// definition is compared. Exact matches are treated as already present and only the incoming surplus is reported as a conflict.
    /// </summary>
    Task<BondImportResult> ImportOwnerEntries(int userId, IEnumerable<BondEntryImport> entries, CancellationToken cancellationToken);

    /// <summary>
    /// Adds what is still missing for the incoming rows the owner accepted: every submitted row, including the
    /// occurrences that already exist, so a retry after a partial commit adds only the remaining occurrences.
    /// </summary>
    Task<BondImportResult> ApplyOwnerConflicts(int userId, IEnumerable<BondEntryImport> acceptedEntries, CancellationToken cancellationToken);
}