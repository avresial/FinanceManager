using FinanceManager.Domain.Entities.Imports;

namespace FinanceManager.Application.Services.Bonds;

public interface IBondAccountImportService
{
    Task ApplyResolvedConflicts(IEnumerable<ResolvedBondImportConflict> resolvedConflicts);
    Task<BondImportResult> ImportEntries(int userId, int accountId, IEnumerable<BondEntryImport> entries);
}