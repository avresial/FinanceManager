using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.MoneyFlow.Spending;

public class ExpenseDistributionService(IEnumerable<IExpenseDistributionServiceTyped> typedServices) : IExpenseDistributionService
{
    public async Task<List<NameValueResult>> GetExpenseDistribution(int userId, Currency currency, DateTime start, DateTime end)
    {
        Dictionary<string, decimal> aggregated = [];

        foreach (var service in typedServices)
        {
            foreach (var result in await service.GetExpenseDistribution(userId, currency, start, end))
            {
                aggregated[result.Name] = aggregated.GetValueOrDefault(result.Name) + result.Value;
            }
        }

        return aggregated
            .Select(kv => new NameValueResult(kv.Key, Math.Round(kv.Value, 2)))
            .OrderBy(r => r.Name)
            .ToList();
    }
}