namespace FinanceManager.Domain.Entities.Imports;

public record StockEntryImport(DateTime PostingDate, decimal ValueChange, string Ticker);