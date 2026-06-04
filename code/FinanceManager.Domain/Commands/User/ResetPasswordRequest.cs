namespace FinanceManager.Application.Commands.User;

public record ResetPasswordRequest(string Token, string NewPassword);