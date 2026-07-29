namespace FinanceManager.Domain.Labels.Entities;

public class RecurringSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public string MerchantKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsFlaggedForReview { get; set; }
}