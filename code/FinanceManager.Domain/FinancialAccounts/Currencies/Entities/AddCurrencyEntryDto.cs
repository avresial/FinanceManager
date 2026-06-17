using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

public record AddCurrencyEntryDto(DateTime PostingDate, decimal ValueChange, string Description, string? ContractorDetails, List<FinancialLabel> Labels);