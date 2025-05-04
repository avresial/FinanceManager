using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.User;
public class UserDetails
{
    public int Id { get; set; }
    public required string Login { get; set; }
    public PricingLevel PricingLevel { get; set; }
    public RecordCapacity RecordCapacity { get; set; }
}
