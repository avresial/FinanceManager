using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Shared;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FinanceManager.Application.Assets.Pricing;

/// <summary>
/// Pulls quotes for an <see cref="AssetListing"/> from its enabled <see cref="MarketDataSymbol"/>s through
/// the <see cref="IStockPriceSource"/> fallback chain, normalises them with the listing's
/// <see cref="AssetListing.PriceMultiplier"/> (e.g. GBX → GBP) and stores them as <see cref="PriceQuote"/> rows.
/// </summary>
public class PriceQuoteIngestionService(
    IMarketDataSymbolRepository symbolRepository,
    IPriceQuoteRepository priceQuoteRepository,
    IStockPriceSource priceSource,
    ICurrencyRepository currencyRepository,
    ILogger<PriceQuoteIngestionService> logger) : IPriceQuoteIngestionService
{
    private static readonly TimeSpan _failedFetchCooldown = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fetchLocks = new();

    // Minor "pence"-style quote units collapse to their major currency once PriceMultiplier is
    // applied (1 GBX = 0.01 GBP), so FX conversion can use a currency the rate provider knows.
    private static readonly IReadOnlyDictionary<string, string> _minorToMajorCurrency = DefaultCurrency.MinorQuoteUnits;

    public async Task FetchAndStoreAsync(AssetListing listing, DateTime start, DateTime end, CancellationToken ct = default)
    {
        // Trim the window to trading days before touching the provider chain: a window that is all
        // weekend has nothing to return, and a weekend edge only widens the request for free.
        if (!MarketCalendar.HasMarketDay(start, end))
        {
            logger.LogDebug(
                "Skipping price fetch for listing {ListingId}: {Start:yyyy-MM-dd}..{End:yyyy-MM-dd} contains no trading day.",
                listing.Id, start.Date, end.Date);
            return;
        }

        start = MarketCalendar.NextMarketDay(start);
        end = MarketCalendar.LastMarketDay(end);

        var symbols = listing.MarketDataSymbols
            .Where(s => s.IsEnabled)
            .OrderBy(s => priceSource.SupportsProviderSelection ? priceSource.GetPriority(s.Provider) : 0)
            .ThenByDescending(s => s.IsPrimary)
            .ThenBy(s => s.Id)
            .ToList();

        if (symbols.Count == 0)
        {
            logger.LogDebug("Listing {ListingId} has no enabled market-data symbol; cannot fetch prices", listing.Id);
            return;
        }

        if (!priceSource.SupportsProviderSelection)
            symbols = [symbols[0]];

        // One lock per listing serialises every provider symbol for the same instrument.
        var fetchKey = $"INVEST_FETCH_{listing.Id}";
        var fetchLock = _fetchLocks.GetOrAdd(fetchKey, _ => new SemaphoreSlim(1, 1));
        await fetchLock.WaitAsync(ct);
        try
        {
            // Re-check after acquiring the lock so a concurrent fetch isn't repeated.
            var existing = await priceQuoteRepository.GetRange(
                listing.Id, PriceQuoteCoverage.DayStart(start), PriceQuoteCoverage.DayEnd(end), ct);
            if (!PriceQuoteCoverage.NeedsFetch(existing, start.Date, end.Date))
                return;

            foreach (var symbol in symbols)
            {
                if (await TryFetchFromSymbolAsync(listing, symbol, start, end, ct))
                    return;
            }
        }
        finally
        {
            fetchLock.Release();
        }
    }

    /// <summary>Returns true when quotes were stored from <paramref name="symbol"/>; false moves on to the next source.</summary>
    private async Task<bool> TryFetchFromSymbolAsync(
        AssetListing listing,
        MarketDataSymbol symbol,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        if (symbol.LastError is not null && DateTimeOffset.UtcNow - symbol.UpdatedAt < _failedFetchCooldown)
        {
            logger.LogDebug(
                "Skipping price fetch for listing {ListingId} provider {Provider}: last attempt failed at {UpdatedAt}.",
                listing.Id, symbol.Provider, symbol.UpdatedAt);
            return false;
        }

        var rawCurrencyName = string.IsNullOrWhiteSpace(symbol.Currency) ? listing.TradingCurrency : symbol.Currency;
        if (string.IsNullOrWhiteSpace(rawCurrencyName))
        {
            logger.LogWarning(
                "Skipping price fetch for listing {ListingId} provider {Provider}: neither symbol nor listing currency is configured.",
                listing.Id,
                symbol.Provider);
            return false;
        }

        var rawCurrency = await currencyRepository.GetOrAdd(rawCurrencyName, rawCurrencyName, ct);

        IReadOnlyList<StockPrice> prices;
        try
        {
            prices = priceSource.SupportsProviderSelection
                ? await priceSource.GetDailySeries(symbol.Provider, symbol.Symbol, string.Empty, start, end, rawCurrency, ct)
                : await priceSource.GetDailySeries(symbol.Symbol, string.Empty, start, end, rawCurrency, ct);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Price fetch cancelled for listing {ListingId} provider {Provider} symbol {Symbol}.",
                listing.Id, symbol.Provider, symbol.Symbol);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Price fetch cancelled or timed out for listing {ListingId} provider {Provider} symbol {Symbol}; trying the next source.",
                listing.Id, symbol.Provider, symbol.Symbol);
            await symbolRepository.RecordFetchResult(symbol.Id, null, ex.Message, ct);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Price fetch failed for listing {ListingId} provider {Provider} symbol {Symbol}",
                listing.Id, symbol.Provider, symbol.Symbol);
            await symbolRepository.RecordFetchResult(symbol.Id, null, ex.Message, ct);
            return false;
        }

        if (prices.Count == 0)
        {
            await symbolRepository.RecordFetchResult(symbol.Id, null, "No price data returned", ct);
            return false;
        }

        var normalizedCurrencyName = NormalizedCurrencyName(rawCurrencyName, listing.PriceMultiplier);
        var quotes = prices
            .Where(price => price.PricePerUnit > 0)
            .Select(price => new PriceQuote
            {
                AssetListingId = listing.Id,
                MarketDataSymbolId = symbol.Id,
                Provider = symbol.Provider,
                Price = price.PricePerUnit * listing.PriceMultiplier,
                Currency = normalizedCurrencyName,
                PriceTime = PriceQuoteCoverage.DayStart(price.Date),
                QuoteType = PriceQuoteType.EndOfDay,
                RawPrice = price.PricePerUnit,
                RawCurrency = rawCurrencyName,
                FetchedAt = DateTimeOffset.UtcNow
            })
            .ToList();

        if (quotes.Count == 0)
            return false;

        await priceQuoteRepository.UpsertRange(quotes, ct);
        await symbolRepository.RecordFetchResult(symbol.Id, DateTimeOffset.UtcNow, null, ct);
        return true;
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
}