using FinanceManager.Domain.Entities.Imports;

namespace FinanceManager.Application.Services.Stocks;

public interface IStockAccountImportService
{
    Task ApplyResolvedConflicts(IEnumerable<ResolvedStockImportConflict> resolvedConflicts);
    Task<StockImportResult> ImportEntries(int userId, int accountId, IEnumerable<StockEntryImport> entries);
}