using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace FinanceManager.Application.Providers;

public class StockPriceProvider(
    IStockPriceRepository stockRepository,
    IAlphaVantageClient apiClient,
    IStockDetailsRepository stockDetailsRepository,
    ICurrencyRepository currencyRepository,
    ICurrencyExchangeService currencyExchangeService,
    IMemoryCache cache,
    IIsinResolver isinResolver) : IStockPriceProvider
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _detailsLocks = new();

    public async Task<decimal> GetPricePerUnitAsync(string ticker, Currency targetCurrency, DateTime asOf)
    {
        if (string.IsNullOrWhiteSpace(ticker)) throw new ArgumentException("{ticker}", nameof(ticker));

        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        var isin = await isinResolver.ResolveAsync(normalizedTicker, ct: CancellationToken.None);
        if (isin is null) return 0m;

        var key = $"STOCK_PRICE_{targetCurrency.ShortName}_{asOf:yyyyMMdd}_{isin}";
        if (cache.TryGetValue(key, out decimal cached)) return cached;

        var stockDetails = await stockDetailsRepository.Get(isin) ??
                   await ResolveStockDetailsAsync(normalizedTicker, isin, CancellationToken.None);

        if (stockDetails is null)
            return 0m;

        StockPrice? stockPrice = null;
        var originalKey = $"STOCK_PRICE_{stockDetails.Currency.ShortName}_{asOf:yyyyMMdd}_{isin}";
        if (cache.TryGetValue(originalKey, out StockPrice? cachedOriginal))
            stockPrice = cachedOriginal;

        if (stockPrice is null)
        {
            var priceLock = _locks.GetOrAdd(originalKey, _ => new SemaphoreSlim(1, 1));
            await priceLock.WaitAsync();
            try
            {
                if (cache.TryGetValue(originalKey, out cachedOriginal))
                {
                    stockPrice = cachedOriginal;
                }
                else
                {
                    stockPrice = await stockRepository.GetThisOrNextOlder(isin, asOf);
                    if (stockPrice is null)
                    {
                        var fetched = await TryFetchFromApiAsync(normalizedTicker, isin, asOf.AddDays(-7), asOf, CancellationToken.None);
                        stockPrice = fetched?.Where(x => x.Date.Date <= asOf.Date).OrderByDescending(x => x.Date).FirstOrDefault();
                    }

                    if (stockPrice is not null && stockPrice.PricePerUnit > 0)
                        cache.Set(originalKey, stockPrice, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(60) });
                }
            }
            finally
            {
                priceLock.Release();
            }
        }

        decimal price = 0m;
        if (stockPrice is not null)
        {
            if (stockPrice.Currency == targetCurrency)
                price = stockPrice.PricePerUnit;
            else
                price = (await currencyExchangeService.GetPricePerUnit(stockPrice, targetCurrency, asOf)) ?? 0m;
        }

        if (price > 0)
            cache.Set(key, price, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(60) });

        return price;
    }

    public async Task<IReadOnlyList<StockPrice>> GetPricesAsync(string ticker, DateTime start, DateTime end, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || start == default || end == default) return [];
        if (end < start) return [];

        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        var isin = await isinResolver.ResolveAsync(normalizedTicker, ct: ct);
        if (isin is null) return [];

        IReadOnlyList<StockPrice> existing = [];

        if (start.Date == end.Date)
        {
            var price = await stockRepository.Get(isin, start);
            if (price is not null)
                return [price];
        }
        else
        {
            existing = await stockRepository.GetRange(isin, start, end);
        }

        if (NeedsFetch(existing, start, end))
        {
            var fetched = await TryFetchFromApiAsync(normalizedTicker, isin, start, end, ct);
            if (fetched.Count > 0)
                existing = MergeByDate(existing, fetched);
        }

        return existing.Where(x => x.Date >= start && x.Date <= end).OrderByDescending(x => x.Date).ToList();
    }

    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetPricePerUnitSeriesAsync(
        string ticker,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || start == default || end == default) return new Dictionary<DateTime, decimal>();
        if (end < start) return new Dictionary<DateTime, decimal>();

        var startDate = start.Date;
        var endDate = end.Date;
        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        var isin = await isinResolver.ResolveAsync(normalizedTicker, ct: ct);
        if (isin is null) return new Dictionary<DateTime, decimal>();

        var inRangePrices = await GetPricesAsync(normalizedTicker, startDate, endDate, ct);
        var latestByDate = inRangePrices
            .GroupBy(x => x.Date.Date)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.Date).First());

        var seedPrice = await stockRepository.GetThisOrNextOlder(isin, startDate);
        var series = new Dictionary<DateTime, decimal>((endDate - startDate).Days + 1);
        StockPrice? latestKnownPrice = seedPrice;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (latestByDate.TryGetValue(date, out var todayPrice))
                latestKnownPrice = todayPrice;

            if (latestKnownPrice is null) continue;

            var convertedPrice = latestKnownPrice.Currency == targetCurrency
                ? latestKnownPrice.PricePerUnit
                : (await currencyExchangeService.GetPricePerUnit(latestKnownPrice, targetCurrency, date)) ?? 0m;

            if (convertedPrice > 0)
                series[date] = convertedPrice;
        }

        return series;
    }

    private async Task<IReadOnlyList<StockPrice>> TryFetchFromApiAsync(string ticker, string isin, DateTime start, DateTime end, CancellationToken ct)
    {
        var fetchKey = $"STOCK_AV_FETCH_{isin}_{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fetchLock = _locks.GetOrAdd(fetchKey, _ => new SemaphoreSlim(1, 1));
        await fetchLock.WaitAsync(ct);
        try
        {
            if (start.Date == end.Date)
            {
                var existingSingle = await stockRepository.Get(isin, start);
                if (existingSingle is not null)
                    return [existingSingle];
            }
            else
            {
                var existingRange = await stockRepository.GetRange(isin, start, end);
                if (existingRange is not null && !NeedsFetch(existingRange, start, end))
                    return existingRange;
            }

            var details = await ResolveStockDetailsAsync(ticker, isin, ct);
            if (details is null)
                return [];

            var apiPrices = await apiClient.GetDailySeries(ticker, isin, start, end, details.Currency, ct);
            if (apiPrices is not null && apiPrices.Count > 0)
                await stockRepository.Add(apiPrices);

            return apiPrices ?? [];
        }
        finally
        {
            fetchLock.Release();
        }
    }

    private async Task<StockDetails> ResolveStockDetailsAsync(string ticker, string isin, CancellationToken ct)
    {
        var existing = await stockDetailsRepository.Get(isin, ct);
        if (existing is not null && !NeedsEnrichment(existing))
            return existing;

        var detailsLock = _detailsLocks.GetOrAdd(isin, _ => new SemaphoreSlim(1, 1));
        await detailsLock.WaitAsync(ct);
        try
        {
            existing = await stockDetailsRepository.Get(isin, ct);
            if (existing is not null && !NeedsEnrichment(existing))
                return existing;

            var matches = await apiClient.SearchTicker(ticker, ct);
            var exact = matches?.FirstOrDefault(x => string.Equals(x.Symbol, ticker, StringComparison.OrdinalIgnoreCase));
            var selected = exact ?? matches?.FirstOrDefault();

            Currency currency;
            if (selected is not null && !string.IsNullOrWhiteSpace(selected.Currency))
                currency = await currencyRepository.GetOrAdd(selected.Currency, selected.Currency, ct);
            else
                currency = existing?.Currency ?? await currencyRepository.GetOrAdd(DefaultCurrency.USD.ShortName, DefaultCurrency.USD.Symbol, ct);

            var details = new StockDetails
            {
                Isin = isin,
                Ticker = ticker,
                Name = selected?.Name ?? existing?.Name ?? string.Empty,
                Type = selected?.Type ?? existing?.Type ?? string.Empty,
                Region = selected?.Region ?? existing?.Region ?? string.Empty,
                Currency = currency
            };

            return await stockDetailsRepository.Add(details, ct);
        }
        finally
        {
            detailsLock.Release();
        }
    }

    private static bool NeedsEnrichment(StockDetails details)
        => string.IsNullOrWhiteSpace(details.Name)
           || string.IsNullOrWhiteSpace(details.Type)
           || string.IsNullOrWhiteSpace(details.Region);

    private static bool NeedsFetch(IReadOnlyList<StockPrice> existing, DateTime start, DateTime end)
    {
        if (existing is null || existing.Count == 0) return true;
        if (existing.Count == 1 && start.Date == end.Date) return false;

        var existingDates = existing.Select(x => x.Date.Date).ToHashSet();
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (!existingDates.Contains(date)) return true;
        }

        return false;
    }

    private static IReadOnlyList<StockPrice> MergeByDate(IReadOnlyList<StockPrice> existing, IReadOnlyList<StockPrice> fetched)
    {
        return existing.Concat(fetched)
            .GroupBy(x => x.Date.Date)
            .Select(x => x.OrderByDescending(p => p.Date).First())
            .ToList();
    }
}