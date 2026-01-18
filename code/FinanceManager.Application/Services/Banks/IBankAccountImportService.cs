using FinanceManager.Domain.Entities.Imports;

namespace FinanceManager.Application.Services;

public interface IBankAccountImportService
{
    Task ApplyResolvedConflicts(IEnumerable<ResolvedImportConflict> resolvedConflicts);
    Task<ImportResult> ImportEntries(int userId, int accountId, IEnumerable<CurrencyEntryImport> entries);
}