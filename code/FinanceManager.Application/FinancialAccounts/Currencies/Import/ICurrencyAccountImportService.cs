using FinanceManager.Domain.FinancialAccounts.Currencies.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Import;

public interface ICurrencyAccountImportService
{
    Task ApplyResolvedConflicts(IEnumerable<ResolvedImportConflict> resolvedConflicts);
    Task ApplyResolvedConflicts(IEnumerable<ResolvedImportConflict> resolvedConflicts, CancellationToken cancellationToken);
    Task<ImportResult> ImportEntries(
        int userId,
        int accountId,
        IEnumerable<CurrencyEntryImport> entries,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts = null,
        Func<int, int, int, Task>? onProgress = null);
    Task<ImportResult> ImportEntries(
        int userId,
        int accountId,
        IEnumerable<CurrencyEntryImport> entries,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<ImportConflict>, Task>? onConflicts = null,
        Func<int, int, int, Task>? onProgress = null);
}