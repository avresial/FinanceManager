namespace FinanceManager.Domain.Dtos;

public record StockDataImportDto(int AccountId, IReadOnlyList<StockEntryImportRecordDto> Entries);