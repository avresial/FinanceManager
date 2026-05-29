using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public async Task Add(RefreshToken token)
    {
        await context.RefreshTokens.AddAsync(token);
        await context.SaveChangesAsync();
    }

    public Task<RefreshToken?> GetByHash(string tokenHash) =>
        context.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

    public async Task Update(RefreshToken token)
    {
        context.RefreshTokens.Update(token);
        await context.SaveChangesAsync();
    }

    public async Task RevokeFamily(Guid familyId, DateTime revokedAt)
    {
        var familyTokens = await context.RefreshTokens
            .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ToListAsync();

        if (familyTokens.Count == 0) return;

        foreach (var token in familyTokens)
            token.RevokedAt = revokedAt;

        await context.SaveChangesAsync();
    }

    public async Task<int> RemoveExpired(DateTime utcNow)
    {
        var expired = await context.RefreshTokens
            .Where(x => x.AbsoluteExpiresAt < utcNow)
            .ToListAsync();

        if (expired.Count == 0) return 0;

        context.RefreshTokens.RemoveRange(expired);
        await context.SaveChangesAsync();
        return expired.Count;
    }
}