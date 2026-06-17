using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Infrastructure.Dtos;

public class UserDto
{
    public int Id { get; set; }
    public required string Login { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public required string Password { get; set; }
    public PricingLevel PricingLevel { get; set; } = PricingLevel.Free;
    public UserRole UserRole { get; set; } = UserRole.User;
    public required DateTime CreationDate { get; set; }

    /// <summary>Consecutive failed login attempts since the last successful login or lockout reset.</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>When set and still in the future, logins for this account are refused until this UTC instant.</summary>
    public DateTime? LockoutEndUtc { get; set; }


    public User ToUser() => new()
    {
        UserId = Id,
        Login = Login,
        FirstName = FirstName,
        LastName = LastName,
        PricingLevel = PricingLevel,
        UserRole = UserRole,
        CreationDate = CreationDate
    };

}