using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Domain.Transactions.Cash.Entities;

public record AddCurrencyEntryDto(DateTime PostingDate, decimal ValueChange, string Description, string? ContractorDetails, List<FinancialLabel> Labels);