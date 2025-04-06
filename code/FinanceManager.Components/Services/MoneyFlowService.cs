using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;

namespace FinanceManager.Components.Services;

public class MoneyFlowService(HttpClient httpClient) : IMoneyFlowService
{
    private readonly HttpClient? _httpClient = httpClient;

    public Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, DateTime start, DateTime end, InvestmentType investmentType)
    {
        throw new NotImplementedException();
    }

    public Task<List<AssetEntry>> GetEndAssetsPerAcount(int userId, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public Task<List<AssetEntry>> GetEndAssetsPerType(int userId, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public Task<List<TimeSeriesModel>> GetIncome(int userId, DateTime start, DateTime end, TimeSpan? step = null)
    {
        throw new NotImplementedException();
    }

    public Task<decimal?> GetNetWorth(int userId, DateTime date)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public Task<List<TimeSeriesModel>> GetSpending(int userId, DateTime start, DateTime end, TimeSpan? step = null)
    {
        throw new NotImplementedException();
    }
}
