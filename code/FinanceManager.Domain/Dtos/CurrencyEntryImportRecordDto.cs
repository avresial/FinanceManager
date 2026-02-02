namespace FinanceManager.Infrastructure.Dtos;

public record CurrencyEntryImportRecordDto(DateTime PostingDate, decimal ValueChange, string? ContractorDetails = null);