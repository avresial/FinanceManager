using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;

namespace FinanceManager.Application.FinancialAccounts.Bond.Import;

public interface IBondAccountImportService
{
    Task ApplyResolvedConflicts(IEnumerable<ResolvedBondImportConflict> resolvedConflicts);
    Task ApplyResolvedConflicts(IEnumerable<ResolvedBondImportConflict> resolvedConflicts, CancellationToken cancellationToken);
    Task<BondImportResult> ImportEntries(int userId, int accountId, IEnumerable<BondEntryImport> entries);
    Task<BondImportResult> ImportEntries(int userId, int accountId, IEnumerable<BondEntryImport> entries, CancellationToken cancellationToken);
}