namespace FinanceManager.Domain.Identity.Services;

public enum RefreshTokenStatus
{
    Success,
    NotFound,
    Expired,
    Revoked
}

/// <summary>
/// Outcome of validating and rotating a refresh token. On <see cref="RefreshTokenStatus.Success"/> the caller
/// receives the user the token belongs to and a freshly minted replacement token to hand back to the client.
/// </summary>
public record RefreshTokenRotationResult(RefreshTokenStatus Status, int UserId, string? NewRefreshToken)
{
    public static RefreshTokenRotationResult Failure(RefreshTokenStatus status) => new(status, 0, null);
    public static RefreshTokenRotationResult Ok(int userId, string newRefreshToken) =>
        new(RefreshTokenStatus.Success, userId, newRefreshToken);
}

public interface IRefreshTokenService
{
    /// <summary>Issues a brand-new refresh token family for the user and returns the raw token to store in a cookie.</summary>
    Task<string> Issue(int userId, CancellationToken cancellationToken = default);

    /// <summary>Validates the presented raw token, rotating it on success. Replaying a rotated token revokes its family.</summary>
    Task<RefreshTokenRotationResult> ValidateAndRotate(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes the presented raw token (used on explicit logout). No-op if it is unknown or already revoked.</summary>
    Task Revoke(string rawToken, CancellationToken cancellationToken = default);
}