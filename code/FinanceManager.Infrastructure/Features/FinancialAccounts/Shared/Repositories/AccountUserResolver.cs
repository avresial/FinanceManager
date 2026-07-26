using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

/// <summary>
/// Resolves <c>accountId → userId</c> against the Accounts table, caching the result. Ownership is stable
/// for the life of an account, so the mapping is cached with a long expiration and no per-user tag — it is
/// never invalidated by entry writes.
/// </summary>
public class AccountUserResolver(AppDbContext context, HybridCache cache) : IAccountUserResolver
{
    private static readonly HybridCacheEntryOptions _cacheOptions = new() { Expiration = TimeSpan.FromMinutes(30) };

    public ValueTask<int?> GetUserId(int accountId, CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync<int?>(
            $"acc:owner:a{accountId}",
            async ct => await context.Accounts.AsNoTracking()
                .Where(a => a.AccountId == accountId)
                .Select(a => (int?)a.UserId)
                .FirstOrDefaultAsync(ct),
            _cacheOptions,
            cancellationToken: cancellationToken);
}