namespace FinanceManager.Domain.Dtos;

public record BondDataImportDto(int AccountId, IReadOnlyList<BondEntryImportRecordDto> Entries);