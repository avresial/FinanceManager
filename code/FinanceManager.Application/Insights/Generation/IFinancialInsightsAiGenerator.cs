using FinanceManager.Domain.Entities.Users;

namespace FinanceManager.Application.Insights.Generation;

public interface IFinancialInsightsAiGenerator
{
    Task<List<FinancialInsight>> GenerateInsights(int userId, int? accountId, int count, CancellationToken cancellationToken = default);
}