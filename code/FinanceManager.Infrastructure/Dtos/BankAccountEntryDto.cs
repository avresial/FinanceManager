using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos;

public class BankAccountEntryDto : FinancialEntryBaseDto
{
    public string Description { get; set; } = string.Empty;
    public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;
}
