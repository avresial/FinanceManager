namespace FinanceManager.Domain.FinancialAccounts.Currencies.Imports;

public record CurrencyEntryImport(DateTime PostingDate, decimal ValueChange, string? ContractorDetails = null, string? Description = null);