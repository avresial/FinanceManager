using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities.Accounts
{
    public record AddBankEntryDto(DateTime PostingDate, decimal ValueChange, ExpenseType ExpenseType, string Description);
}
