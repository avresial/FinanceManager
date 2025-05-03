using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Commands.Login;

public class LoginResponseModel
{
    public required string UserName { get; set; }
    public required UserRole UserRole { get; set; }
    public required int UserId { get; set; }
    public required string AccessToken { get; set; }
    public int ExpiresIn { get; set; }
}
