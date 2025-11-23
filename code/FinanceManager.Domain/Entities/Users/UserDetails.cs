using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Users;
public class UserDetails
{
    public int UserId { get; set; }
    public required string Login { get; set; }
    public PricingLevel PricingLevel { get; set; }
    public required RecordCapacity RecordCapacity { get; set; }
}
