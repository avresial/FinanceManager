using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
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

    public async Task<bool> TryConsume(string tokenHash, DateTime usedAt)
    {
        // A single conditional UPDATE is atomic on relational providers, so concurrent redemptions race on the
        // database and only one flips UsedAt from null — the losers affect zero rows and forfeit the claim. This
        // is what makes the token genuinely single-use even when the same link is submitted twice at once.
        if (context.Database.IsRelational())
        {
            var affected = await context.PasswordResetTokens
                .Where(x => x.TokenHash == tokenHash && x.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, usedAt));
            return affected > 0;
        }

        // The in-memory provider used by tests doesn't support ExecuteUpdate and only ever runs single-threaded,
        // so the lost-update race the atomic path guards against can't occur here.
        var token = await context.PasswordResetTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.UsedAt == null);
        if (token is null) return false;

        token.UsedAt = usedAt;
        await context.SaveChangesAsync();
        return true;
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