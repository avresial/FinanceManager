using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Users;

public class User
{
    public int UserId { get; set; }
    public required string Login { get; set; }
    public PricingLevel PricingLevel { get; set; } = PricingLevel.Free;
    public UserRole UserRole { get; set; } = UserRole.User;
    public required DateTime CreationDate { get; set; }
}
