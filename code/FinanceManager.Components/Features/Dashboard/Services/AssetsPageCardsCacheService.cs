using Blazored.LocalStorage;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Services;

public class AssetsPageCardsCacheService(
    ILocalStorageService localStorageService,
    IMemoryCache memoryCache,
    AssetsHttpClient assetsHttpClient,
    ILogger<AssetsPageCardsCacheService> logger)
    : LocalStorageStateCacheService<AssetsPageCardsCacheSnapshot, AssetsPageCardsRefreshContext, string>(
        localStorageService,
        memoryCache,
        logger,
        _cacheKeyPrefix)
{
    private readonly Dictionary<(int UserId, int CurrencyId, DateTime Start, DateTime End), Task<List<TimeSeriesModel>>> _timeSeriesRequests = [];

    // Share only overlapping requests. A later visit always starts a fresh request, regardless of the TTL cache.
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

    // Share only requests that overlap with the aggregate load; never reuse a completed visit.
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

    private const string _cacheKeyPrefix = "assets-page-cards-cache-v1";
    private static readonly TimeSpan _maxStale = TimeSpan.FromMinutes(5);

    public Task<AssetsPageCardsCacheSnapshot> GetSnapshotAsync(AssetsPageCardsRefreshContext context)
        => GetOrRefreshAsync(context);

    protected override string GetCacheKey(AssetsPageCardsRefreshContext refreshContext)
        => BuildCacheKey(refreshContext.UserId, refreshContext.CurrencyId, refreshContext.StartDateTime, refreshContext.EndDateTime);

    protected override async Task<AssetsPageCardsCacheSnapshot> BuildStateAsync(AssetsPageCardsRefreshContext refreshContext)
    {
        var startDate = refreshContext.StartDateTime.Date;
        var endDate = refreshContext.EndDateTime;

        // Only the id crosses the wire, so the requested currency can be rebuilt from the context.
        var currency = new Currency { Id = refreshContext.CurrencyId };
        var assetsTimeSeriesTask = GetFreshTimeSeriesAsync(refreshContext);
        var distributionTask = GetFreshDistributionAsync(refreshContext);
        var returnsTask = GetFreshReturnsAsync(refreshContext);
        await Task.WhenAll(assetsTimeSeriesTask, distributionTask, returnsTask);

        return new AssetsPageCardsCacheSnapshot
        {
            SchemaVersion = AssetsPageCardsCacheSnapshot.CurrentSchemaVersion,
            UserId = refreshContext.UserId,
            CurrencyId = refreshContext.CurrencyId,
            StartDateTime = startDate,
            EndDateTime = endDate,
            FetchedAtUtc = DateTime.UtcNow,
            AssetsTimeSeries = [.. (await assetsTimeSeriesTask)],
            EndAssetsPerType = [.. (await distributionTask).TypeData],
            EndAssetsPerAccount = [.. (await distributionTask).AccountData],
            MoneyWeightedReturn = (await returnsTask).MoneyWeightedReturn,
            TimeWeightedReturn = (await returnsTask).TimeWeightedReturn,
            ReturnAttribution = (await returnsTask).Attribution,
        };
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

    protected override bool IsUsable(AssetsPageCardsCacheSnapshot? state, string cacheKey, DateTime utcNow)
    {
        if (state is null)
            return false;

        if (state.SchemaVersion != AssetsPageCardsCacheSnapshot.CurrentSchemaVersion)
            return false;

        if (state.MoneyWeightedReturn is null || state.TimeWeightedReturn is null || state.ReturnAttribution is null)
            return false;

        if (utcNow - state.FetchedAtUtc > _maxStale)
            return false;

        return BuildCacheKey(state.UserId, state.CurrencyId, state.StartDateTime, state.EndDateTime) == cacheKey;
    }

    // Presets clamp the range end to the current clock, so only the day of each bound reaches the key —
    // see CacheKeyDate. One entry per picked range is intended here; one per visit is not.
    private static string BuildCacheKey(int userId, int currencyId, DateTime startDateTime, DateTime endDateTime)
        => $"{userId}:{currencyId}:{CacheKeyDate.ToSegment(startDateTime)}:{CacheKeyDate.ToSegment(endDateTime)}";
}