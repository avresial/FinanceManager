using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Accounts
{
    public record AddBankEntryDto(DateTime PostingDate, decimal ValueChange, ExpenseType ExpenseType, string Description, List<FinancialLabel> labels);
}
