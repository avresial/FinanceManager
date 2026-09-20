using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IPortfolioReturnAttributionService
{
    Task<PortfolioReturnAttributionResult> GetAsync(
        int userId,
        Currency currency,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default);
}