using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Identity.Entities;

public class User
{
    public int UserId { get; set; }
    public required string Login { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public PricingLevel PricingLevel { get; set; } = PricingLevel.Free;
    public UserRole UserRole { get; set; } = UserRole.User;
    public required DateTime CreationDate { get; set; }
}