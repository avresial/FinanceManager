namespace FinanceManager.Domain.Dashboard.Services;

public interface ICacheInvalidator
{
    ValueTask InvalidateUser(int userId, CancellationToken cancellationToken = default);
}