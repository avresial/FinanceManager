namespace FinanceManager.Domain.Entities.Imports;

public record StockImportResult(int AccountId, int Imported, int Failed, IReadOnlyList<string> Errors, IReadOnlyList<StockImportConflict> Conflicts);