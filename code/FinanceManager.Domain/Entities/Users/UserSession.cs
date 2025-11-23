using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Users;

public class UserSession
{
    public int UserId { get; set; }
    public required string UserName { get; set; }
    public required string Password { get; set; }
    public required UserRole UserRole { get; set; }
    public string Token { get; set; } = string.Empty;
}
