using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.Domain.Entities.Accounts;
public record AddBankEntryDto(DateTime PostingDate, decimal ValueChange, string Description, List<FinancialLabel> Labels);
