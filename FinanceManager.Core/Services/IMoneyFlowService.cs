using FinanceManager.Core.Entities.MoneyFlowModels;
using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Services
{
    public interface IMoneyFlowService
    {
        Task<List<AssetEntry>> GetEndAssetsPerAcount(DateTime start, DateTime end);
        Task<List<AssetEntry>> GetEndAssetsPerType(DateTime start, DateTime end);
        Task<List<TimeSeriesModel>> GetAssetsTimeSeries(DateTime start, DateTime end);
        Task<List<TimeSeriesModel>> GetAssetsTimeSeries(DateTime start, DateTime end, InvestmentType investmentType);
        Task<decimal?> GetNetWorth(DateTime date);
        Task<Dictionary<DateTime, decimal>> GetNetWorth(DateTime start, DateTime end);
    }
}
