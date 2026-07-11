using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Application.Dashboard;

public class CachingDashboardQueryService(
    DashboardQueryService inner,
    HybridCache cache) : IDashboardQueryService
{
    public Task<DashboardOverviewDto?> GetOverview(
        int userId, int currencyId, DateTime start, DateTime end,
        CancellationToken cancellationToken = default)
    {
        var key = $"dash:u{userId}:c{currencyId}:{start:yyyyMMdd}:{end:yyyyMMdd}";
        return cache.GetOrCreateAsync<DashboardOverviewDto?>(
            key,
            ct => new ValueTask<DashboardOverviewDto?>(inner.GetOverview(userId, currencyId, start, end, ct)),
            options: new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            tags: [$"dash:u{userId}"],
            cancellationToken: cancellationToken).AsTask();
    }
}