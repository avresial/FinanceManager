namespace FinanceManager.Domain.Entities.Users;

/// <summary>
/// A persisted, single-use password-reset token. The raw token value is never stored — only its hash — so a
/// database leak cannot be replayed against the reset endpoint. A token is consumed (stamped <see cref="UsedAt"/>)
/// the moment a password is successfully reset, and it stops being valid once <see cref="ExpiresAt"/> passes.
/// </summary>
public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when the token is redeemed; a non-null value means the token has been used and is no longer valid.</summary>
    public DateTime? UsedAt { get; set; }
}