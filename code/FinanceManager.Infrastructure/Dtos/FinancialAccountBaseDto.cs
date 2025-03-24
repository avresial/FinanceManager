namespace FinanceManager.Infrastructure.Dtos;

public class FinancialAccountBaseDto
{
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
};
