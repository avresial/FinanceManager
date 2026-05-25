using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface IDiversificationService
{
    Task<DiversificationScore> GetDiversificationScore(int userId, DateTime asOfDate);
}