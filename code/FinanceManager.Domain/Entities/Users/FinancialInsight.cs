namespace FinanceManager.Domain.Entities.Users;

public class FinancialInsight
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? AccountId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}