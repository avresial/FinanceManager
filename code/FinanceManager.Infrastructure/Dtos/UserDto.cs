using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos;

public class UserDto
{
    public int Id { get; set; }
    public required string Login { get; set; }
    public required string Password { get; set; }
    public PricingLevel PricingLevel { get; set; } = PricingLevel.Free;
    public UserRole UserRole { get; set; } = UserRole.User;
    public required DateTime CreationDate { get; set; }


    public User ToUser() => new()
    {
        UserId = Id,
        Login = Login,
        PricingLevel = PricingLevel,
        UserRole = UserRole,
        CreationDate = CreationDate
    };

}