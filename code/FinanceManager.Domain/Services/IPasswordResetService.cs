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
    /// Issues a raw reset token for the given login. For a registered account this is a single-use, time-limited
    /// token that is persisted; for an unknown login it is a throwaway, non-persisted token of the same shape that
    /// can never validate. Either way a token is always returned to backend code so future email delivery can avoid
    /// leaking an account-enumeration signal; controllers must not expose the raw token over HTTP.
    /// </summary>
    Task<string> RequestReset(string login, CancellationToken cancellationToken = default);

    /// <summary>Validates and consumes the presented raw token, setting the account's password on success.</summary>
    Task<PasswordResetResult> ResetPassword(string rawToken, string newPassword, CancellationToken cancellationToken = default);
}