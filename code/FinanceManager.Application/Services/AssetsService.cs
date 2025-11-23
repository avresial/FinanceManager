using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class AssetsService(IEnumerable<IAssetsServiceTyped> typedAssetServices) : IAssetsService
{
    public async Task<bool> IsAnyAccountWithAssets(int userId)
    {
        foreach (var service in typedAssetServices)
            if (await service.IsAnyAccountWithAssets(userId))
                return true;

        return false;
    }
    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        foreach (var service in typedAssetServices)
            await foreach (var result in service.GetEndAssetsPerAccount(userId, currency, asOfDate))
                yield return result;
    }
    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        foreach (var service in typedAssetServices)
            await foreach (var result in service.GetEndAssetsPerType(userId, currency, asOfDate))
                yield return result;
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];

        foreach (var service in typedAssetServices)
        {
            foreach (var result in await service.GetAssetsTimeSeries(userId, currency, start, end))
            {
                if (prices.ContainsKey(result.DateTime))
                    prices[result.DateTime] += result.Value;
                else
                    prices[result.DateTime] = result.Value;
            }
        }

        var timeBucket = TimeBucketService.Get(prices.Select(x => (x.Key, x.Value))).OrderByDescending(x => x.Date);
        var testResult = timeBucket.Select(x => new TimeSeriesModel() { DateTime = x.Date, Value = x.Objects.Last() }).ToList();

        return timeBucket.Select(x => new TimeSeriesModel() { DateTime = x.Date, Value = x.Objects.Last() }).ToList();
    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        Dictionary<DateTime, decimal> prices = [];
        foreach (var service in typedAssetServices)
        {
            foreach (var result in await service.GetAssetsTimeSeries(userId, currency, start, end, investmentType))
            {
                if (prices.ContainsKey(result.DateTime))
                    prices[result.DateTime] += result.Value;
                else
                    prices[result.DateTime] = result.Value;
            }
        }

        var timeBucket = TimeBucketService.Get(prices.Select(x => (x.Key, x.Value))).OrderByDescending(x => x.Date);
        return timeBucket.Select(x => new TimeSeriesModel() { DateTime = x.Date, Value = x.Objects.Last() }).ToList();
    }
}