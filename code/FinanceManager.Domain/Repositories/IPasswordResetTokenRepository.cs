using FinanceManager.Domain.Entities.Users;

namespace FinanceManager.Domain.Repositories;

public interface IPasswordResetTokenRepository
{
    Task Add(PasswordResetToken token);
    Task<PasswordResetToken?> GetByHash(string tokenHash);
    Task Update(PasswordResetToken token);

    /// <summary>
    /// Marks every still-active (unused) token for a user as used, so requesting a new reset link silently
    /// invalidates any earlier ones — only the most recent link can ever be redeemed.
    /// </summary>
    Task InvalidateActiveTokensForUser(int userId, DateTime usedAt);

    /// <summary>Deletes tokens whose expiry is in the past. Intended for periodic cleanup.</summary>
    Task<int> RemoveExpired(DateTime utcNow);
}