using FinanceManager.Domain.Entities.Imports;

namespace FinanceManager.Application.FinancialAccounts.Stock.Import;

public interface IStockAccountImportService
{
    Task ApplyResolvedConflicts(IEnumerable<ResolvedStockImportConflict> resolvedConflicts);
    Task<StockImportResult> ImportEntries(int userId, int accountId, IEnumerable<StockEntryImport> entries);
}