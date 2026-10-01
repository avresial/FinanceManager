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
        var moneyWeightedReturnTask = GetOptionalAsync(
            () => assetsHttpClient.GetMoneyWeightedReturn(refreshContext.UserId, currency, startDate, endDate),
            "money-weighted return");
        var timeWeightedReturnTask = GetOptionalAsync(
            () => assetsHttpClient.GetTimeWeightedReturn(refreshContext.UserId, currency, startDate, endDate),
            "time-weighted return");
        var returnAttributionTask = GetOptionalAsync(
            () => assetsHttpClient.GetReturnAttribution(refreshContext.UserId, currency, startDate, endDate),
            "return attribution");
        await Task.WhenAll(assetsTimeSeriesTask, distributionTask, moneyWeightedReturnTask, timeWeightedReturnTask, returnAttributionTask);

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
            MoneyWeightedReturn = await moneyWeightedReturnTask,
            TimeWeightedReturn = await timeWeightedReturnTask,
            ReturnAttribution = await returnAttributionTask,
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