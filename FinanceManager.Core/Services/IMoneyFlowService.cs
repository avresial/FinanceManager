using FinanceManager.Core.Entities.MoneyFlowModels;

namespace FinanceManager.Core.Services
{
    public interface IMoneyFlowService
    {
        Task<List<AssetEntry>> GetEndAssetsPerAcount(DateTime start, DateTime end);
        Task<List<AssetEntry>> GetEndAssetsPerType(DateTime start, DateTime end);
        Task<List<TimeSeriesModel>> GetEndAssetsPerTypeTimeSeries(DateTime start, DateTime end);
    }
}
