using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Identity.Repositories;

public interface IRefreshTokenRepository
{
    Task Add(RefreshToken token);
    Task<RefreshToken?> GetByHash(string tokenHash);
    Task Update(RefreshToken token);

    /// <summary>
    /// Atomically revokes one token and inserts its replacement in a single commit, so a rotation can never leave
    /// the old token revoked without a persisted replacement.
    /// </summary>
    Task Rotate(RefreshToken revoked, RefreshToken replacement);

    /// <summary>Revokes every still-active token in a family. Used as the theft response when a rotated token is replayed.</summary>
    Task RevokeFamily(Guid familyId, DateTime revokedAt);

    /// <summary>Deletes tokens whose absolute expiry is in the past. Intended for periodic cleanup.</summary>
    Task<int> RemoveExpired(DateTime utcNow);
}