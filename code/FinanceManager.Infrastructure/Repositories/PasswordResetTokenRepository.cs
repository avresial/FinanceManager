using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class PasswordResetTokenRepository(AppDbContext context) : IPasswordResetTokenRepository
{
    public async Task Add(PasswordResetToken token)
    {
        await context.PasswordResetTokens.AddAsync(token);
        await context.SaveChangesAsync();
    }

    public Task<PasswordResetToken?> GetByHash(string tokenHash) =>
        context.PasswordResetTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

    public async Task Update(PasswordResetToken token)
    {
        context.PasswordResetTokens.Update(token);
        await context.SaveChangesAsync();
    }

    public async Task InvalidateActiveTokensForUser(int userId, DateTime usedAt)
    {
        var activeTokens = await context.PasswordResetTokens
            .Where(x => x.UserId == userId && x.UsedAt == null)
            .ToListAsync();

        if (activeTokens.Count == 0) return;

        foreach (var token in activeTokens)
            token.UsedAt = usedAt;

        await context.SaveChangesAsync();
    }

    public async Task<int> RemoveExpired(DateTime utcNow)
    {
        var expired = await context.PasswordResetTokens
            .Where(x => x.ExpiresAt < utcNow)
            .ToListAsync();

        if (expired.Count == 0) return 0;

        context.PasswordResetTokens.RemoveRange(expired);
        await context.SaveChangesAsync();
        return expired.Count;
    }
}