using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IInvestmentRateService
{
    IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end);
}