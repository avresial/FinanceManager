using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;
public interface ILiabilitiesService
{
    Task<bool> IsAnyAccountWithLiabilities(int userId);
    IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end);
    IAsyncEnumerable<NameValueResult> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end);
    IAsyncEnumerable<TimeSeriesModel> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end);
}
