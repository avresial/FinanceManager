using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos
{
    public class UserDto
    {
        public int Id { get; set; }
        public required string Login { get; set; }
        public required string Password { get; set; }
        public PricingLevel PricingLevel { get; set; } = PricingLevel.Free;

    }
}
