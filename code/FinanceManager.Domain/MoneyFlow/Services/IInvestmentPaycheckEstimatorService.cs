using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IInvestmentPaycheckEstimatorService
{
    Task<InvestmentPaycheckEstimate> GetEstimate(int userId, Currency currency, DateTime asOfDate, decimal annualWithdrawalRate = 0.05m, int salaryMonths = 3);
}