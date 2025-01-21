using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Accounts
{
    public record AddBankEntryDto(DateTime PostingDate, decimal ValueChange, ExpenseType ExpenseType, string Description);
}
