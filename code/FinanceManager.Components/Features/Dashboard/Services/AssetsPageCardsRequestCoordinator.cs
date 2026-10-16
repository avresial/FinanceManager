using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Services;

public class AssetsPageCardsRequestCoordinator(
    AssetsHttpClient assetsHttpClient,
    ILogger<AssetsPageCardsRequestCoordinator> logger)
{
    private readonly Dictionary<(int UserId, int CurrencyId, DateTime Start, DateTime End), Task<List<TimeSeriesModel>>> _timeSeriesRequests = [];

    // Share only overlapping requests. A later visit always starts a fresh request, even for the same context.
    public async Task<List<TimeSeriesModel>> GetFreshTimeSeriesAsync(AssetsPageCardsRefreshContext context)
    {
        var key = (context.UserId, context.CurrencyId, context.StartDateTime.Date, context.EndDateTime);
        Task<List<TimeSeriesModel>> request;
        lock (_timeSeriesRequests)
        {
            if (!_timeSeriesRequests.TryGetValue(key, out request!))
            {
                request = assetsHttpClient.GetAssetsTimeSeries(context.UserId,
                    new Currency { Id = context.CurrencyId }, context.StartDateTime.Date, context.EndDateTime);
                _timeSeriesRequests.Add(key, request);
            }
        }
        try
        {
            return await request;
        }
        finally
        {
            lock (_timeSeriesRequests)
            {
                if (_timeSeriesRequests.TryGetValue(key, out var current) && ReferenceEquals(current, request))
                    _timeSeriesRequests.Remove(key);
            }
        }
    }

    private readonly Dictionary<(int UserId, int CurrencyId, DateTime End), Task<DistributionCardModel>> _distributionRequests = [];

    // Share only overlapping requests; never reuse a completed visit.
    public async Task<DistributionCardModel> GetFreshDistributionAsync(AssetsPageCardsRefreshContext context)
    {
        var key = (context.UserId, context.CurrencyId, context.EndDateTime);
        Task<DistributionCardModel> request;
        lock (_distributionRequests)
        {
            if (!_distributionRequests.TryGetValue(key, out request!))
            {
                request = FetchDistributionAsync(context);
                _distributionRequests.Add(key, request);
            }
        }
        try
        {
            return await request;
        }
        finally
        {
            lock (_distributionRequests)
            {
                if (_distributionRequests.TryGetValue(key, out var current) && ReferenceEquals(current, request))
                    _distributionRequests.Remove(key);
            }
        }
    }

    private async Task<DistributionCardModel> FetchDistributionAsync(AssetsPageCardsRefreshContext context)
    {
        var currency = new Currency { Id = context.CurrencyId };
        var types = assetsHttpClient.GetEndAssetsPerType(context.UserId, currency, context.EndDateTime);
        var accounts = assetsHttpClient.GetEndAssetsPerAccount(context.UserId, currency, context.EndDateTime);
        await Task.WhenAll(types, accounts);
        return new DistributionCardModel(await types, await accounts);
    }

    private readonly Dictionary<(int UserId, int CurrencyId, DateTime Start, DateTime End), Task<PortfolioReturnSourceModel>> _returnRequests = [];

    // Only concurrent callers share a request; every later visit fetches again.
    public async Task<PortfolioReturnSourceModel> GetFreshReturnsAsync(AssetsPageCardsRefreshContext context)
    {
        var key = (context.UserId, context.CurrencyId, context.StartDateTime.Date, context.EndDateTime);
        Task<PortfolioReturnSourceModel> request;
        lock (_returnRequests)
        {
            if (!_returnRequests.TryGetValue(key, out request!))
            {
                request = FetchReturnsAsync(context);
                _returnRequests.Add(key, request);
            }
        }
        try { return await request; }
        finally
        {
            lock (_returnRequests)
            {
                if (_returnRequests.TryGetValue(key, out var current) && ReferenceEquals(current, request))
                    _returnRequests.Remove(key);
            }
        }
    }

    private async Task<PortfolioReturnSourceModel> FetchReturnsAsync(AssetsPageCardsRefreshContext context)
    {
        var currency = new Currency { Id = context.CurrencyId };
        var money = GetOptionalAsync(() => assetsHttpClient.GetMoneyWeightedReturn(context.UserId, currency, context.StartDateTime.Date, context.EndDateTime), "money-weighted return");
        var time = GetOptionalAsync(() => assetsHttpClient.GetTimeWeightedReturn(context.UserId, currency, context.StartDateTime.Date, context.EndDateTime), "time-weighted return");
        var attribution = GetOptionalAsync(() => assetsHttpClient.GetReturnAttribution(context.UserId, currency, context.StartDateTime.Date, context.EndDateTime), "return attribution");
        await Task.WhenAll(money, time, attribution);
        return new(await money, await time, await attribution);
    }

    private async Task<T?> GetOptionalAsync<T>(Func<Task<T>> load, string name) where T : class
    {
        try
        {
            return await load();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not load {Metric} for assets page", name);
            return null;
        }
    }
}