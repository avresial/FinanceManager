namespace FinanceManager.Domain.Dtos;

public record StockEntryImportRecordDto(DateTime PostingDate, decimal ValueChange, string Ticker);