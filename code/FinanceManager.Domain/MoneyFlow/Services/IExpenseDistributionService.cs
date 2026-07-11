using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IExpenseDistributionService
{
    Task<List<NameValueResult>> GetExpenseDistribution(int userId, Currency currency, DateTime start, DateTime end);
}