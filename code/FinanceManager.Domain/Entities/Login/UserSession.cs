using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Login;

public class UserSession
{
    public int UserId { get; set; }
    public UserRole UserRole { get; set; }
    public required string UserName { get; set; }
    public required string Password { get; set; }
    public string Token { get; set; } = string.Empty;
}
