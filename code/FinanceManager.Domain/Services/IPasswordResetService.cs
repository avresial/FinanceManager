namespace FinanceManager.Domain.Services;

public enum PasswordResetStatus
{
    Success,
    InvalidToken,
    Expired,
    AlreadyUsed
}

/// <summary>Outcome of redeeming a password-reset token.</summary>
public record PasswordResetResult(PasswordResetStatus Status)
{
    public bool Succeeded => Status == PasswordResetStatus.Success;

    public static PasswordResetResult Ok() => new(PasswordResetStatus.Success);
    public static PasswordResetResult Failure(PasswordResetStatus status) => new(status);
}

public interface IPasswordResetService
{
    /// <summary>
    /// Issues a single-use, time-limited reset token for the account with the given login and returns the raw
    /// token, or <c>null</c> when no such account exists. Returning <c>null</c> for unknown logins lets the API
    /// keep its public response identical whether or not the account exists, avoiding account enumeration.
    /// </summary>
    Task<string?> RequestReset(string login, CancellationToken cancellationToken = default);

    /// <summary>Validates and consumes the presented raw token, setting the account's password on success.</summary>
    Task<PasswordResetResult> ResetPassword(string rawToken, string newPassword, CancellationToken cancellationToken = default);
}