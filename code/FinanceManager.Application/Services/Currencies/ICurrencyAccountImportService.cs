using FinanceManager.Domain.Entities.Imports;

namespace FinanceManager.Application.Services.Currencies;

public interface ICurrencyAccountImportService
{
    Task ApplyResolvedConflicts(IEnumerable<ResolvedImportConflict> resolvedConflicts);
    Task<ImportResult> ImportEntries(int userId, int accountId, IEnumerable<CurrencyEntryImport> entries);
}