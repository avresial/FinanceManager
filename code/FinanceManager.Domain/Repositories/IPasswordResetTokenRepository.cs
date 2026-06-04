using FinanceManager.Domain.Entities.Users;

namespace FinanceManager.Domain.Repositories;

public interface IPasswordResetTokenRepository
{
    Task Add(PasswordResetToken token);
    Task<PasswordResetToken?> GetByHash(string tokenHash);

    /// <summary>
    /// Atomically marks the token with the given hash as used, but only if it is still unused. Returns
    /// <c>true</c> only for the single caller that wins the claim, so a token can be redeemed exactly once even
    /// when concurrent requests present the same link.
    /// </summary>
    Task<bool> TryConsume(string tokenHash, DateTime usedAt);

    /// <summary>
    /// Marks every still-active (unused) token for a user as used, so requesting a new reset link silently
    /// invalidates any earlier ones — only the most recent link can ever be redeemed.
    /// </summary>
    Task InvalidateActiveTokensForUser(int userId, DateTime usedAt);

    /// <summary>Deletes tokens whose expiry is in the past. Intended for periodic cleanup.</summary>
    Task<int> RemoveExpired(DateTime utcNow);
}