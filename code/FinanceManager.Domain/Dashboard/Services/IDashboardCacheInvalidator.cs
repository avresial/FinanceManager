namespace FinanceManager.Domain.Dashboard.Services;

public interface IDashboardCacheInvalidator
{
    ValueTask InvalidateUser(int userId, CancellationToken cancellationToken = default);
}