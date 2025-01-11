using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Services
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
