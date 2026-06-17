namespace FinanceManager.Domain.Identity.Entities;

/// <summary>
/// A persisted, rotating refresh token. The raw token value is never stored — only its hash — so a database leak
/// cannot be replayed against the refresh endpoint. Tokens are grouped into a <see cref="FamilyId"/> so that
/// presenting an already-rotated (and therefore revoked) token can revoke the whole family as a theft signal.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string TokenHash { get; set; }
    public Guid FamilyId { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Sliding expiry; extended on each rotation but never past <see cref="AbsoluteExpiresAt"/>.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Hard cap for the whole family, set when the family is first issued and copied unchanged on rotation.</summary>
    public DateTime AbsoluteExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}