using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface IDiversificationService
{
    Task<DiversificationScore> GetDiversificationScore(int userId, DateTime asOfDate);

    Task<DiversificationBreakdown> GetDiversificationBreakdown(int userId, DateTime asOfDate, CancellationToken cancellationToken = default);
}