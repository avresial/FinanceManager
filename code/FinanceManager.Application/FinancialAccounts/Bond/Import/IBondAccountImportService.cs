using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;

namespace FinanceManager.Application.FinancialAccounts.Bond.Import;

public interface IBondAccountImportService
{
    Task ApplyResolvedConflicts(IEnumerable<ResolvedBondImportConflict> resolvedConflicts);
    Task<BondImportResult> ImportEntries(int userId, int accountId, IEnumerable<BondEntryImport> entries);
}