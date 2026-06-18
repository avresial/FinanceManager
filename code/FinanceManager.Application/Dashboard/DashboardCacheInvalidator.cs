using FinanceManager.Domain.Dashboard.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Application.Dashboard;

public class DashboardCacheInvalidator(HybridCache cache) : IDashboardCacheInvalidator
{
    public ValueTask InvalidateUser(int userId, CancellationToken cancellationToken = default) =>
        cache.RemoveByTagAsync($"dash:u{userId}", cancellationToken);
}