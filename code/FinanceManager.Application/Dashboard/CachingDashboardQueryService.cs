using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Application.Dashboard;

public class CachingDashboardQueryService(
    DashboardQueryService inner,
    HybridCache cache) : IDashboardQueryService
{
    public async Task<DashboardOverviewDto?> GetOverview(
        int userId, int currencyId, DateTime start, DateTime end,
        CancellationToken cancellationToken = default)
    {
        var key = $"dash:u{userId}:c{currencyId}:{start:O}:{end:O}";
        var loaded = false;
        var overview = await cache.GetOrCreateAsync<DashboardOverviewDto?>(
            key,
            async ct =>
            {
                var result = await inner.GetOverview(userId, currencyId, start, end, ct);
                loaded = true;
                return result;
            },
            options: new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            tags: [$"dash:u{userId}"],
            cancellationToken: cancellationToken);

        // A price-provider refresh can change valuation without a user write/tag invalidation.
        // Cache misses already valued the distribution; hits revalidate only that surface.
        return overview is null || loaded
            ? overview
            : await inner.RefreshAssetsDistribution(overview, cancellationToken);
    }
}