using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FinanceManager.Application.Assets.Pricing;

/// <summary>
/// Prices an <see cref="AssetListing"/> on the new asset model. Serves cached
/// <see cref="PriceQuote"/> rows first, fetches from the listing's primary enabled
/// <see cref="MarketDataSymbol"/> through the existing <see cref="IStockPriceSource"/> fallback
/// chain when data is missing, normalises the raw quote with the listing's
/// <see cref="AssetListing.PriceMultiplier"/> (e.g. GBX → GBP) and converts to the target currency.
/// </summary>
public class InvestmentPriceProvider(
    IAssetListingRepository listingRepository,
    IMarketDataSymbolRepository symbolRepository,
    IPriceQuoteRepository priceQuoteRepository,
    IStockPriceSource priceSource,
    ICurrencyRepository currencyRepository,
    ICurrencyExchangeService currencyExchangeService,
    IMemoryCache cache,
    ILogger<InvestmentPriceProvider> logger) : IInvestmentPriceProvider
{
    private const int _fetchLookbackDays = 7;
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan _failedFetchCooldown = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fetchLocks = new();

    // Minor "pence"-style quote units collapse to their major currency once PriceMultiplier is
    // applied (1 GBX = 0.01 GBP), so FX conversion can use a currency the rate provider knows.
    private static readonly IReadOnlyDictionary<string, string> _minorToMajorCurrency = DefaultCurrency.MinorQuoteUnits;

    public async Task<decimal> GetPricePerUnitAsync(long assetListingId, Currency targetCurrency, DateTime asOf, CancellationToken ct = default)
    {
        if (assetListingId <= 0) return 0m;

        // Cache-first: a hit must not depend on any repository I/O.
        var cacheKey = $"INVEST_PRICE_{targetCurrency.ShortName}_{asOf:yyyyMMdd}_{assetListingId}";
        if (cache.TryGetValue(cacheKey, out decimal cached)) return cached;

        var listing = await listingRepository.Get(assetListingId, ct);
        if (listing is null) return 0m;

        var quote = await priceQuoteRepository.GetLatestOnOrBefore(assetListingId, DayEnd(asOf), cancellationToken: ct);
        if (quote is null)
        {
            await TryFetchAndStoreAsync(listing, asOf.AddDays(-_fetchLookbackDays), asOf, ct);
            quote = await priceQuoteRepository.GetLatestOnOrBefore(assetListingId, DayEnd(asOf), cancellationToken: ct);
        }

        if (quote is null) return 0m;

        var (price, direct) = await ConvertAsync(quote.Price, quote.Currency, targetCurrency, asOf, ct);

        // A USD-fallback value is not denominated in the target currency, so caching it under the
        // target-currency key would serve mislabelled amounts for the whole TTL.
        if (price > 0 && direct)
            cache.Set(cacheKey, price, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheTtl });

        return price;
    }

    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetPricePerUnitSeriesAsync(
        long assetListingId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        if (assetListingId <= 0 || start == default || end == default || end < start)
            return new Dictionary<DateTime, decimal>();

        var listing = await listingRepository.Get(assetListingId, ct);
        if (listing is null) return new Dictionary<DateTime, decimal>();

        var startDate = start.Date;
        var endDate = end.Date;

        var existing = await priceQuoteRepository.GetRange(assetListingId, DayStart(startDate), DayEnd(endDate), ct);
        if (NeedsFetch(existing, startDate, endDate))
        {
            await TryFetchAndStoreAsync(listing, startDate, endDate, ct);
            existing = await priceQuoteRepository.GetRange(assetListingId, DayStart(startDate), DayEnd(endDate), ct);
        }

        var latestByDate = existing
            .GroupBy(x => x.PriceTime.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PriceTime).First());

        var seed = await priceQuoteRepository.GetLatestOnOrBefore(assetListingId, DayStart(startDate), cancellationToken: ct);

        // Prefetch FX rates once per distinct source currency over the whole range instead of
        // issuing one (uncached) conversion per day inside the loop.
        var ratesByCurrency = new Dictionary<string, Dictionary<DateTime, decimal>>(StringComparer.OrdinalIgnoreCase);
        var sourceCurrencyNames = latestByDate.Values
            .Select(q => q.Currency)
            .Concat(seed is not null ? [seed.Currency] : [])
            .Where(name => !string.Equals(name, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in sourceCurrencyNames)
        {
            var fromCurrency = await currencyRepository.GetOrAdd(name, name, ct);
            var rates = await currencyExchangeService.GetExchangeRateAsync(fromCurrency, targetCurrency, startDate, endDate);
            var rateMap = new Dictionary<DateTime, decimal>(rates.Count);
            foreach (var (rateDate, value) in rates)
            {
                if (value is not null)
                    rateMap[rateDate.Date] = value.Value;
            }

            FillRateGaps(rateMap, startDate, endDate);

            // A currency with no rate anywhere in the range gets one whole-range fallback
            // conversion (which may cross through USD), never one lookup per day.
            if (rateMap.Count == 0)
            {
                var (unitRate, _) = await ConvertAsync(1m, name, targetCurrency, endDate, ct);
                if (unitRate > 0)
                {
                    for (var date = startDate; date <= endDate; date = date.AddDays(1))
                        rateMap[date] = unitRate;
                }
            }

            ratesByCurrency[name] = rateMap;
        }

        var series = new Dictionary<DateTime, decimal>((endDate - startDate).Days + 1);
        var latestKnown = seed;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (latestByDate.TryGetValue(date, out var todayQuote))
                latestKnown = todayQuote;

            if (latestKnown is null) continue;

            // Days whose rate is still unknown after prefetch + gap filling are skipped rather
            // than resolved individually: a per-day fallback multiplies the full provider chain
            // by the range length and is what pushed chart requests past the client timeout.
            decimal converted = 0m;
            if (string.Equals(latestKnown.Currency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
                converted = latestKnown.Price;
            else if (ratesByCurrency.TryGetValue(latestKnown.Currency, out var rateMap) && rateMap.TryGetValue(date, out var rate))
                converted = latestKnown.Price * rate;

            if (converted > 0)
                series[date] = converted;
        }

        return series;
    }

    public async Task<bool> EnsureQuotesAsync(long assetListingId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        if (assetListingId <= 0 || start == default || end == default || end < start)
            return false;

        var listing = await listingRepository.Get(assetListingId, ct);
        if (listing is null) return false;

        var startDate = start.Date;
        var endDate = end.Date;

        // Coverage check before the fetch path, mirroring GetPricePerUnitSeriesAsync: an
        // already-covered listing skips the per-symbol lock and the provider chain entirely.
        var existing = await priceQuoteRepository.GetRange(assetListingId, DayStart(startDate), DayEnd(endDate), ct);
        if (!NeedsFetch(existing, startDate, endDate))
            return existing.Count > 0;

        await TryFetchAndStoreAsync(listing, startDate, endDate, ct);

        existing = await priceQuoteRepository.GetRange(assetListingId, DayStart(startDate), DayEnd(endDate), ct);
        return existing.Count > 0;
    }

    // FX gaps (weekends, holidays, dates past the exchange service's per-call resolution cap)
    // reuse the nearest known rate in the range: forward-fill first, then backfill the leading
    // edge from the earliest known rate.
    private static void FillRateGaps(Dictionary<DateTime, decimal> rateMap, DateTime startDate, DateTime endDate)
    {
        if (rateMap.Count == 0) return;

        decimal? carried = null;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (rateMap.TryGetValue(date, out var known))
                carried = known;
            else if (carried is decimal rate)
                rateMap[date] = rate;
        }

        var earliest = rateMap[rateMap.Keys.Min()];
        for (var date = startDate; date <= endDate && !rateMap.ContainsKey(date); date = date.AddDays(1))
            rateMap[date] = earliest;
    }

    // Direct is false when the returned value is the USD fallback (i.e. not denominated in
    // targetCurrency); such values must not be cached under the target currency.
    private async Task<(decimal Price, bool Direct)> ConvertAsync(decimal price, string sourceCurrencyName, Currency targetCurrency, DateTime date, CancellationToken ct)
    {
        if (price <= 0) return (0m, true);
        if (string.Equals(sourceCurrencyName, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
            return (price, true);

        if (date > DateTime.UtcNow) date = DateTime.UtcNow;
        var sourceCurrency = await currencyRepository.GetOrAdd(sourceCurrencyName, sourceCurrencyName, ct);
        var rate = await currencyExchangeService.GetExchangeRateAsync(sourceCurrency, targetCurrency, date.Date);
        if (rate is decimal value) return (price * value, true);

        // The preferred currency could not be reached; fall back to USD so the position keeps a
        // value instead of collapsing to zero.
        logger.LogWarning("No exchange rate {From}→{To} on {Date}; falling back to USD", sourceCurrency.ShortName, targetCurrency.ShortName, date.Date);

        if (string.Equals(sourceCurrencyName, DefaultCurrency.USD.ShortName, StringComparison.OrdinalIgnoreCase))
            return (price, false);

        var usdRate = await currencyExchangeService.GetExchangeRateAsync(sourceCurrency, DefaultCurrency.USD, date.Date);
        return usdRate is decimal toUsd ? (price * toUsd, false) : (0m, false);
    }

    private async Task TryFetchAndStoreAsync(AssetListing listing, DateTime start, DateTime end, CancellationToken ct)
    {
        var symbol = listing.MarketDataSymbols
            .Where(s => s.IsEnabled)
            .OrderByDescending(s => s.IsPrimary)
            .ThenBy(s => s.Id)
            .FirstOrDefault();

        if (symbol is null)
        {
            logger.LogDebug("Listing {ListingId} has no enabled market-data symbol; cannot fetch prices", listing.Id);
            return;
        }

        // A symbol whose most recent attempt failed keeps failing for a while (bad symbol,
        // rate-limited provider). Without a cooldown every chart request would re-run the whole
        // provider fallback chain for it, adding external-call latency to each page load.
        if (symbol.LastError is not null && DateTimeOffset.UtcNow - symbol.UpdatedAt < _failedFetchCooldown)
        {
            logger.LogDebug("Skipping price fetch for listing {ListingId}: last attempt failed at {UpdatedAt} and is within the cooldown", listing.Id, symbol.UpdatedAt);
            return;
        }

        // Key the lock by listing+symbol only: _fetchLocks is static and never evicts, so per-range
        // keys would leak a SemaphoreSlim per requested window. Sharing one lock per symbol also
        // serialises overlapping range fetches, which is what we want.
        var fetchKey = $"INVEST_FETCH_{listing.Id}_{symbol.Id}";
        var fetchLock = _fetchLocks.GetOrAdd(fetchKey, _ => new SemaphoreSlim(1, 1));
        await fetchLock.WaitAsync(ct);
        try
        {
            // Re-check after acquiring the lock so a concurrent fetch isn't repeated.
            var existing = await priceQuoteRepository.GetRange(listing.Id, DayStart(start), DayEnd(end), ct);
            if (!NeedsFetch(existing, start.Date, end.Date))
                return;

            var rawCurrencyName = string.IsNullOrWhiteSpace(symbol.Currency) ? listing.TradingCurrency : symbol.Currency;
            var rawCurrency = await currencyRepository.GetOrAdd(rawCurrencyName, rawCurrencyName, ct);

            IReadOnlyList<Domain.FinancialAccounts.Investments.Entities.StockPrice> prices;
            try
            {
                prices = await priceSource.GetDailySeries(symbol.Symbol, string.Empty, start, end, rawCurrency, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Price fetch failed for listing {ListingId} symbol {Symbol}", listing.Id, symbol.Symbol);
                await symbolRepository.RecordFetchResult(symbol.Id, null, ex.Message, ct);
                return;
            }

            if (prices.Count == 0)
            {
                await symbolRepository.RecordFetchResult(symbol.Id, null, "No price data returned", ct);
                return;
            }

            var normalizedCurrencyName = NormalizedCurrencyName(rawCurrencyName, listing.PriceMultiplier);
            var quotes = new List<PriceQuote>(prices.Count);
            foreach (var price in prices)
            {
                if (price.PricePerUnit <= 0) continue;

                quotes.Add(new PriceQuote
                {
                    AssetListingId = listing.Id,
                    MarketDataSymbolId = symbol.Id,
                    Provider = symbol.Provider,
                    Price = price.PricePerUnit * listing.PriceMultiplier,
                    Currency = normalizedCurrencyName,
                    PriceTime = DayStart(price.Date),
                    QuoteType = PriceQuoteType.EndOfDay,
                    RawPrice = price.PricePerUnit,
                    RawCurrency = rawCurrencyName,
                    FetchedAt = DateTimeOffset.UtcNow
                });
            }

            if (quotes.Count > 0)
                await priceQuoteRepository.UpsertRange(quotes, ct);

            await symbolRepository.RecordFetchResult(symbol.Id, DateTimeOffset.UtcNow, null, ct);
        }
        finally
        {
            fetchLock.Release();
        }
    }

    // Normalise from the currency the prices were actually fetched in (symbol currency, falling
    // back to the listing's trading currency), so PriceQuote.Currency matches RawCurrency.
    private static string NormalizedCurrencyName(string rawCurrencyName, decimal priceMultiplier)
    {
        if (priceMultiplier == 1m)
            return rawCurrencyName;

        return _minorToMajorCurrency.TryGetValue(rawCurrencyName, out var major)
            ? major
            : rawCurrencyName;
    }

    private static bool NeedsFetch(IReadOnlyList<PriceQuote> existing, DateTime start, DateTime end)
    {
        if (existing is null || existing.Count == 0) return true;

        var existingDates = existing.Select(x => x.PriceTime.Date).ToHashSet();

        // Boundary-coverage check using the 7-day lookback tolerance: ensure data exists
        // within [start, start+7d] and [end-7d, end]. This allows for market holidays and
        // weekends without triggering a full refetch, while genuine gaps at boundaries do.
        var startBoundary = start.AddDays(_fetchLookbackDays);
        var hasStartCoverage = existingDates.Any(d => d >= start && d <= startBoundary);

        var endBoundary = end.AddDays(-_fetchLookbackDays);
        var hasEndCoverage = existingDates.Any(d => d >= endBoundary && d <= end);

        return !hasStartCoverage || !hasEndCoverage;
    }

    private static DateTimeOffset DayStart(DateTime date) => new(date.Date, TimeSpan.Zero);

    private static DateTimeOffset DayEnd(DateTime date) => new DateTimeOffset(date.Date, TimeSpan.Zero).AddDays(1).AddTicks(-1);
}