using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.Entities.FinancialAccounts.Currency;

public record AddCurrencyEntryDto(DateTime PostingDate, decimal ValueChange, string Description, List<FinancialLabel> Labels);