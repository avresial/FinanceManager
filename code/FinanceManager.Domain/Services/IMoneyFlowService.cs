using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Services;
public interface IMoneyFlowService
{
    Task<bool> IsAnyAccountWithAssets(int userId);
    Task<List<NameValueResult>> GetEndAssetsPerAccount(int userId, string currency, DateTime start, DateTime end);
    Task<List<NameValueResult>> GetEndAssetsPerType(int userId, string currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end, InvestmentType investmentType);
    Task<decimal?> GetNetWorth(int userId, string currency, DateTime date);
    Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, string currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetIncome(int userId, string currency, DateTime start, DateTime end, TimeSpan? step = null);
    Task<List<TimeSeriesModel>> GetSpending(int userId, string currency, DateTime start, DateTime end, TimeSpan? step = null);
    Task<List<TimeSeriesModel>> GetBalance(int userId, string currency, DateTime start, DateTime end, TimeSpan? step = null);
    Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end, TimeSpan? step = null);
}
