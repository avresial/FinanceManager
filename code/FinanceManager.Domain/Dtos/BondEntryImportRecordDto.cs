namespace FinanceManager.Domain.Dtos;

public record BondEntryImportRecordDto(DateTime PostingDate, decimal ValueChange, int BondDetailsId);
