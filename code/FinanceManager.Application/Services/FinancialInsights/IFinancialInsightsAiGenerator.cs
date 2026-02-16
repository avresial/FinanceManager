using FinanceManager.Domain.Entities.Users;

namespace FinanceManager.Application.Services.FinancialInsights;

public interface IFinancialInsightsAiGenerator
{
    Task<List<FinancialInsight>> GenerateInsights(int userId, int? accountId, int count, CancellationToken cancellationToken = default);
}
