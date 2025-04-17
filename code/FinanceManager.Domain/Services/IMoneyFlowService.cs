using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Services;

public interface IMoneyFlowService
{
    Task<bool> IsAnyAccountWithAssets(int userId);
    Task<bool> IsAnyAccountWithLiabilities(int userId);
    Task<List<AssetEntry>> GetEndAssetsPerAcount(int userId, DateTime start, DateTime end);
    Task<List<AssetEntry>> GetEndAssetsPerType(int userId, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, DateTime start, DateTime end, InvestmentType investmentType);
    Task<decimal?> GetNetWorth(int userId, DateTime date);
    Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetIncome(int userId, DateTime start, DateTime end, TimeSpan? step = null);
    Task<List<TimeSeriesModel>> GetSpending(int userId, DateTime start, DateTime end, TimeSpan? step = null);
}
