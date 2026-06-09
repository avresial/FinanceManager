namespace FinanceManager.Domain.Commands.User;

/// <summary>
/// Response to a forgot-password request. <see cref="Message"/> is intentionally identical whether or not the
/// account exists, so the response leaks no account-enumeration signal.
/// </summary>
public record ForgotPasswordResponse(string Message);