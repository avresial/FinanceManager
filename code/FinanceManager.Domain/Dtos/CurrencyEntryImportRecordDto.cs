namespace FinanceManager.Domain.Dtos;

public record CurrencyEntryImportRecordDto(DateTime PostingDate, decimal ValueChange, string? ContractorDetails = null, string? Description = null);