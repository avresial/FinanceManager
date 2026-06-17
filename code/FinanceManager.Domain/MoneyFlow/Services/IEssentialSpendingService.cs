using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IEssentialSpendingService
{
    Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds);
}