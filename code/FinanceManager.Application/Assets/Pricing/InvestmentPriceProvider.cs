using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using static FinanceManager.Application.Assets.Pricing.PriceQuoteCoverage;

namespace FinanceManager.Application.Assets.Pricing;

/// <summary>
/// Prices an <see cref="AssetListing"/> on the new asset model. Serves cached
/// <see cref="PriceQuote"/> rows first, asks <see cref="IPriceQuoteIngestionService"/> to fetch
/// when data is missing, and converts the stored quote to the target currency.
/// </summary>
public class InvestmentPriceProvider(
    IAssetListingRepository listingRepository,
    IPriceQuoteRepository priceQuoteRepository,
    IPriceQuoteIngestionService quoteIngestion,
    ICurrencyRepository currencyRepository,
    ICurrencyExchangeService currencyExchangeService,
    IMemoryCache cache,
    ILogger<InvestmentPriceProvider> logger,
    IGuestSessionAccessor? guestSessionAccessor = null) : IInvestmentPriceProvider
{
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(60);

    public async Task<decimal> GetPricePerUnitAsync(long assetListingId, Currency targetCurrency, DateTime asOf, CancellationToken ct = default) =>
        (await GetPricePerUnitResultAsync(assetListingId, targetCurrency, asOf, ct)).Price ?? 0m;

    public async Task<InvestmentPriceResult> GetPricePerUnitResultAsync(
        long assetListingId,
        Currency targetCurrency,
        DateTime asOf,
        CancellationToken ct = default)
    {
        if (assetListingId <= 0) return InvestmentPriceResult.NotFound();

        // Cache-first: a hit must not depend on any repository I/O.
        // Guest databases reuse listing ids but contain independently seeded quotes.
        var cacheScope = guestSessionAccessor?.IsGuest == true
            ? $"GUEST_{guestSessionAccessor.GuestUserId}_"
            : string.Empty;
        var cacheKey = $"INVEST_PRICE_{cacheScope}{targetCurrency.ShortName}_{asOf:yyyyMMdd}_{assetListingId}";
        if (cache.TryGetValue(cacheKey, out decimal cached)) return InvestmentPriceResult.Success(cached);

        var listing = await listingRepository.Get(assetListingId, ct);
        if (listing is null) return InvestmentPriceResult.NotFound();

        // Exchanges are closed at the weekend, so a Saturday/Sunday "as of" is priced from that
        // week's Friday close rather than from dates no provider will ever publish.
        var priceDate = MarketCalendar.LastMarketDay(asOf);

        var quote = await priceQuoteRepository.GetLatestOnOrBefore(assetListingId, DayEnd(priceDate), cancellationToken: ct);
        if (quote is null)
        {
            await quoteIngestion.FetchAndStoreAsync(listing, priceDate.AddDays(-FetchLookbackDays), priceDate, ct);
            quote = await priceQuoteRepository.GetLatestOnOrBefore(assetListingId, DayEnd(priceDate), cancellationToken: ct);
        }

        if (quote is null) return InvestmentPriceResult.NotFound();

        var (price, direct, pending) = await ConvertAsync(quote.Price, quote.Currency, targetCurrency, asOf, ct);
        if (pending is not null)
            return InvestmentPriceResult.NotYetPublished(pending.Message!, pending.RetryAtUtc!.Value);

        // A USD-fallback value is not denominated in the target currency, so caching it under the
        // target-currency key would serve mislabelled amounts for the whole TTL. A pending current-
        // day rate returns above without a numeric fallback.
        if (price > 0 && direct)
            cache.Set(cacheKey, price, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheTtl });

        return price > 0 ? InvestmentPriceResult.Success(price) : InvestmentPriceResult.NotFound();
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
            await quoteIngestion.FetchAndStoreAsync(listing, startDate, endDate, ct);
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
                var (unitRate, _, _) = await ConvertAsync(1m, name, targetCurrency, endDate, ct);
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

        // The closing value must use the same quote, cache scope and exact FX resolution as
        // point valuation. A range may have carried FX after its provider budget was exhausted.
        if (latestKnown is not null)
            series[endDate] = await GetPricePerUnitAsync(assetListingId, targetCurrency, end, ct);

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

        // A window with no trading day at all — a weekend restart of the startup backfill — is
        // served by the last close before it. Reporting it as uncovered would make every listing
        // look like a gap and send the whole portfolio through the provider chain for nothing.
        if (!MarketCalendar.HasMarketDay(startDate, endDate))
            return await priceQuoteRepository.GetLatestOnOrBefore(assetListingId, DayEnd(endDate), cancellationToken: ct) is not null;

        // Coverage check before the fetch path, mirroring GetPricePerUnitSeriesAsync: an
        // already-covered listing skips the per-symbol lock and the provider chain entirely.
        var existing = await priceQuoteRepository.GetRange(assetListingId, DayStart(startDate), DayEnd(endDate), ct);
        if (!NeedsFetch(existing, startDate, endDate))
            return existing.Count > 0;

        await quoteIngestion.FetchAndStoreAsync(listing, startDate, endDate, ct);

        existing = await priceQuoteRepository.GetRange(assetListingId, DayStart(startDate), DayEnd(endDate), ct);
        return existing.Count > 0;
    }

    // FX gaps (weekends, holidays, dates past the exchange service's per-call resolution cap)
    // reuse the nearest known rate in the range: forward-fill first, then backfill the leading
    // edge from the earliest known rate.
    private static void FillRateGaps(Dictionary<DateTime, decimal> rateMap, DateTime startDate, DateTime endDate)
    {
        if (rateMap.Count == 0) return;

        var todayUtc = DateTime.UtcNow.Date;
        decimal? carried = null;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (rateMap.TryGetValue(date, out var known))
                carried = known;
            else if (date.Date != todayUtc && carried is decimal rate)
                rateMap[date] = rate;
        }

        var earliest = rateMap[rateMap.Keys.Min()];
        for (var date = startDate; date <= endDate && !rateMap.ContainsKey(date); date = date.AddDays(1))
            rateMap[date] = earliest;
    }

    // Direct is false when the returned value is the USD fallback (i.e. not denominated in
    // targetCurrency); such values must not be cached under the target currency.
    private async Task<(decimal Price, bool Direct, CurrencyExchangeRateResult? Pending)> ConvertAsync(
        decimal price,
        string sourceCurrencyName,
        Currency targetCurrency,
        DateTime date,
        CancellationToken ct)
    {
        if (price <= 0) return (0m, true, null);
        if (string.Equals(sourceCurrencyName, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
            return (price, true, null);

        if (date > DateTime.UtcNow) date = DateTime.UtcNow;
        var sourceCurrency = await currencyRepository.GetOrAdd(sourceCurrencyName, sourceCurrencyName, ct);
        var rateResult = await currencyExchangeService.GetExchangeRateResultAsync(sourceCurrency, targetCurrency, date.Date);
        if (rateResult.IsSuccess) return (price * rateResult.Value!.Value, true, null);

        if (rateResult.Status == CurrencyExchangeRateStatus.NotYetPublished)
        {
            logger.LogInformation("{Message}", rateResult.Message);
            return (0m, true, rateResult);
        }

        // Historical or otherwise unknown rates retain the existing USD fallback. A current-day
        // publication miss is handled above so the caller gets an explicit retry message instead.
        logger.LogWarning("No exchange rate {From}→{To} on {Date}; falling back to USD", sourceCurrency.ShortName, targetCurrency.ShortName, date.Date);

        if (string.Equals(sourceCurrencyName, DefaultCurrency.USD.ShortName, StringComparison.OrdinalIgnoreCase))
            return (price, false, null);

        var usdRateResult = await currencyExchangeService.GetExchangeRateResultAsync(sourceCurrency, DefaultCurrency.USD, date.Date);
        if (usdRateResult.Status == CurrencyExchangeRateStatus.NotYetPublished)
        {
            logger.LogInformation("{Message}", usdRateResult.Message);
            return (0m, false, usdRateResult);
        }

        return usdRateResult.IsSuccess
            ? (price * usdRateResult.Value!.Value, false, null)
            : (0m, false, null);
    }
}