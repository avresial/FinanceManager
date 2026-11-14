namespace FinanceManager.Domain.Identity.Services;

/// <summary>
/// Outcome of validating and rotating a refresh token. Only <see cref="Rotated"/> carries data, so a successful
/// rotation always has the owning user and a freshly minted replacement token, and a failure never does.
/// </summary>
public closed record RefreshTokenRotationResult
{
    /// <summary>The token was valid and has been replaced by <paramref name="NewRefreshToken"/>.</summary>
    public sealed record Rotated(int UserId, string NewRefreshToken) : RefreshTokenRotationResult;

    /// <summary>No stored token matches the presented value.</summary>
    public sealed record NotFound : RefreshTokenRotationResult;

    /// <summary>The token passed its sliding or absolute expiry.</summary>
    public sealed record Expired : RefreshTokenRotationResult;

    /// <summary>An already-rotated token was replayed; its whole family has been revoked.</summary>
    public sealed record Revoked : RefreshTokenRotationResult;
}