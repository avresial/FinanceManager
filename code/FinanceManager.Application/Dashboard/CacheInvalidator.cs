using FinanceManager.Domain.Dashboard.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Application.Dashboard;

public class CacheInvalidator(HybridCache cache) : ICacheInvalidator
{
    // An entry mutation affects both the owner's dashboard and other derived financial reads.
    public ValueTask InvalidateUser(int userId, CancellationToken cancellationToken = default) =>
        cache.RemoveByTagAsync([$"dash:u{userId}", $"global:u{userId}"], cancellationToken);
}