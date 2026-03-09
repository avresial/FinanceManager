using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class BalanceService(IEnumerable<IBalanceServiceTyped> typedBalanceServices) : IBalanceService
{
    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        Aggregate(service => service.GetInflow(userId, currency, start, end));

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        Aggregate(service => service.GetInflow(userId, currency, start, end, accountIds));

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        Aggregate(service => service.GetOutflow(userId, currency, start, end));

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        Aggregate(service => service.GetOutflow(userId, currency, start, end, accountIds));

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end) =>
        Aggregate(service => service.GetNetCashFlow(userId, currency, start, end));

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        Aggregate(service => service.GetNetCashFlow(userId, currency, start, end, accountIds));

    public Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end) =>
        Aggregate(service => service.GetClosingBalance(userId, currency, start, end));

    public Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        Aggregate(service => service.GetClosingBalance(userId, currency, start, end, accountIds));

    private async Task<List<TimeSeriesModel>> Aggregate(Func<IBalanceServiceTyped, Task<List<TimeSeriesModel>>> getter)
    {
        Dictionary<DateTime, decimal> aggregated = [];

        foreach (var service in typedBalanceServices)
        {
            foreach (var point in await getter(service))
            {
                if (aggregated.ContainsKey(point.DateTime))
                    aggregated[point.DateTime] += point.Value;
                else
                    aggregated[point.DateTime] = point.Value;
            }
        }

        return aggregated.OrderBy(x => x.Key)
                         .Select(x => new TimeSeriesModel(x.Key, x.Value))
                         .ToList();
    }
}