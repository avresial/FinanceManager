using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Insights.Entities;

namespace FinanceManager.Application.Insights.Generation;

public interface IFinancialInsightsAiGenerator
{
    Task<List<FinancialInsight>> GenerateInsights(int userId, int? accountId, int count, CancellationToken cancellationToken = default);
}