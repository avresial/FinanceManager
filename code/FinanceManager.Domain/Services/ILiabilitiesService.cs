using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface ILiabilitiesService
{
    Task<bool> IsAnyAccountWithLiabilities(int userId);
    Task<List<PieChartModel>> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end);
    Task<List<PieChartModel>> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end);
}
