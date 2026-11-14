namespace FinanceManager.Domain.Identity.Services;

public interface IRefreshTokenService
{
    /// <summary>Issues a brand-new refresh token family for the user and returns the raw token to store in a cookie.</summary>
    Task<string> Issue(int userId, CancellationToken cancellationToken = default);

    /// <summary>Validates the presented raw token, rotating it on success. Replaying a rotated token revokes its family.</summary>
    Task<RefreshTokenRotationResult> ValidateAndRotate(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes the presented raw token (used on explicit logout). No-op if it is unknown or already revoked.</summary>
    Task Revoke(string rawToken, CancellationToken cancellationToken = default);
}