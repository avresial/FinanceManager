using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface IInvestmentPaycheckEstimatorService
{
    Task<InvestmentPaycheckEstimate> GetEstimate(int userId, Currency currency, DateTime asOfDate, decimal annualWithdrawalRate = 0.05m, int salaryMonths = 3);
}