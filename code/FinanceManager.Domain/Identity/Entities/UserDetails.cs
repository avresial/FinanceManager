using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Identity.Entities;

public class UserDetails
{
    public int UserId { get; set; }
    public required string Login { get; set; }
    public PricingLevel PricingLevel { get; set; }
    public required RecordCapacity RecordCapacity { get; set; }

    /// <summary>Most recent login time for the user, or <c>null</c> when the user has never logged in.</summary>
    public DateTime? LastLoggedAt { get; set; }
}