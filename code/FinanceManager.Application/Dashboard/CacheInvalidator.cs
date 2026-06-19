using FinanceManager.Domain.Dashboard.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Application.Dashboard;

public class CacheInvalidator(HybridCache cache) : ICacheInvalidator
{
    // The dashboard cache (dash:u{userId}) is derived from a user's account entries, and the global
    // per-user entry caches (global:u{userId}) recalculate running balances on every write, so a single
    // mutation can invalidate both. Busting them together keeps the two namespaces consistent regardless
    // of which write triggered the invalidation. See issue #455.
    public ValueTask InvalidateUser(int userId, CancellationToken cancellationToken = default) =>
        cache.RemoveByTagAsync([$"dash:u{userId}", $"global:u{userId}"], cancellationToken);
}