using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class EssentialSpendingService(IEnumerable<IEssentialSpendingServiceTyped> typedServices) : IEssentialSpendingService
{
    public Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end) =>
        Aggregate(service => service.GetEssentialSpending(userId, currency, start, end));

    public Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        Aggregate(service => service.GetEssentialSpending(userId, currency, start, end, accountIds));

    private async Task<List<TimeSeriesModel>> Aggregate(Func<IEssentialSpendingServiceTyped, Task<List<TimeSeriesModel>>> getter)
    {
        Dictionary<DateTime, decimal> aggregated = [];

        foreach (var service in typedServices)
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