using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;

namespace FinanceManager.Application.FinancialAccounts.Bond.Import;

public interface IBondAccountImportService
{
    Task ApplyResolvedConflicts(IEnumerable<ResolvedBondImportConflict> resolvedConflicts);
    Task<BondImportResult> ImportEntries(int userId, int accountId, IEnumerable<BondEntryImport> entries);
}