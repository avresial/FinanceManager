using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface IInvestmentRateService
{
    IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end);
}