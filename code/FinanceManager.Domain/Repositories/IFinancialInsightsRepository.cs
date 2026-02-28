using FinanceManager.Domain.Entities.Users;

namespace FinanceManager.Domain.Repositories;

public interface IFinancialInsightsRepository
{
    Task<int> GetCountByUser(int userId, CancellationToken cancellationToken = default);
    Task<List<FinancialInsight>> GetLatestByUser(int userId, int count, int? accountId = null, CancellationToken cancellationToken = default);
    Task<bool> AddRange(IEnumerable<FinancialInsight> insights, CancellationToken cancellationToken = default);
}