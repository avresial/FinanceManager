namespace FinanceManager.Domain.Transactions.Cash.Imports;

public record CurrencyEntryImport(DateTime PostingDate, decimal ValueChange, string? ContractorDetails = null, string? Description = null);