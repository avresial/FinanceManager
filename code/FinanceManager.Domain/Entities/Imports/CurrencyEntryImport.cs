namespace FinanceManager.Domain.Entities.Imports;

public record CurrencyEntryImport(DateTime PostingDate, decimal ValueChange, string? ContractorDetails = null);