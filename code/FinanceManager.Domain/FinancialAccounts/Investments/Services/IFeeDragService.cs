using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Services;

/// <summary>
/// Calculates the current annual cost and long-term compound impact of ETF expense ratios.
/// </summary>
public interface IFeeDragService
{
    const decimal MinimumReturnRate = -0.10m;
    const decimal MaximumReturnRate = 0.10m;

    Task<FeeDragAnalysisResult> GetAnalysisAsync(
        int userId,
        Currency currency,
        DateTime asOfDate,
        decimal assumedAnnualReturnRate = 0.07m,
        CancellationToken cancellationToken = default);
}