using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface IExpenseDistributionService
{
    Task<List<NameValueResult>> GetExpenseDistribution(int userId, Currency currency, DateTime start, DateTime end);
}
