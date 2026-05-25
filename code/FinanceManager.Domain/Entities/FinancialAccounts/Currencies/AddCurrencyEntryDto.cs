using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

public record AddCurrencyEntryDto(DateTime PostingDate, decimal ValueChange, string Description, string? ContractorDetails, List<FinancialLabel> Labels);