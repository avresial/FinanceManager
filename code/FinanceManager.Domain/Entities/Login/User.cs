using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Login;

public class User
{
    public int Id { get; set; }
    public required string Login { get; set; }
    public PricingLevel PricingLevel { get; set; } = PricingLevel.Free;
}
