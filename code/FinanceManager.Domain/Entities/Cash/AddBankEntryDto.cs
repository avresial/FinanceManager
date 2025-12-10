using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.Entities.Cash;

public record AddBankEntryDto(DateTime PostingDate, decimal ValueChange, string Description, List<FinancialLabel> Labels);