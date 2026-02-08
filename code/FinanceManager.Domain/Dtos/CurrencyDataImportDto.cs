namespace FinanceManager.Domain.Dtos;

public record CurrencyDataImportDto(int AccountId, IReadOnlyList<CurrencyEntryImportRecordDto> Entries);