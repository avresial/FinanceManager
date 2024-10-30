using FinanceManager.Core.Entities.MoneyFlowModels;
using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Services
{
    public interface IMoneyFlowService
    {
        Task<List<AssetEntry>> GetEndAssetsPerAcount(DateTime start, DateTime end);
        Task<List<AssetEntry>> GetEndAssetsPerType(DateTime start, DateTime end);
        Task<List<TimeSeriesModel>> GetAssetsPerTypeTimeSeries(DateTime start, DateTime end);
        Task<List<TimeSeriesModel>> GetAssetsPerTypeTimeSeries(DateTime start, DateTime end, InvestmentType investmentType);
    }
}
