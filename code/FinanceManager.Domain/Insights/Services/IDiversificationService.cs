using FinanceManager.Domain.Insights.Entities;

namespace FinanceManager.Domain.Insights.Services;

public interface IDiversificationService
{
    Task<DiversificationScore> GetDiversificationScore(int userId, DateTime asOfDate);

    Task<DiversificationBreakdown> GetDiversificationBreakdown(int userId, DateTime asOfDate, CancellationToken cancellationToken = default);
}