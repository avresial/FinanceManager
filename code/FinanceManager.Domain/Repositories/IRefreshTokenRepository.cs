using FinanceManager.Domain.Entities.Users;

namespace FinanceManager.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task Add(RefreshToken token);
    Task<RefreshToken?> GetByHash(string tokenHash);
    Task Update(RefreshToken token);

    /// <summary>Revokes every still-active token in a family. Used as the theft response when a rotated token is replayed.</summary>
    Task RevokeFamily(Guid familyId, DateTime revokedAt);

    /// <summary>Deletes tokens whose absolute expiry is in the past. Intended for periodic cleanup.</summary>
    Task<int> RemoveExpired(DateTime utcNow);
}