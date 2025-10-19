namespace FinanceManager.Infrastructure.Dtos;

public record BankDataImportDto(int AccountId, IReadOnlyList<BankEntryImportRecordDto> Entries);
