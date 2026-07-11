using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Domain.Dashboard.Services;

public interface IDashboardQueryService
{
    /// <summary>
    /// Builds the dashboard overview read model for the given user, currency and
    /// date range. Returns <c>null</c> when the currency cannot be resolved.
    /// </summary>
    Task<DashboardOverviewDto?> GetOverview(int userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}