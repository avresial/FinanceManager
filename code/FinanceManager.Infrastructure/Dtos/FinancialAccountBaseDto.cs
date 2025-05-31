using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos;

public class FinancialAccountBaseDto
{
    public AccountType AccountType { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
};
