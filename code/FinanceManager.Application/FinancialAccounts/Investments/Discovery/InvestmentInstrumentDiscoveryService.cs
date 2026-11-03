using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

/// <summary>
/// Searches OpenFIGI and Alpha Vantage and returns listing-level <see cref="InstrumentDiscoveryResultDto"/>s;
/// <see cref="InstrumentDiscoveryMerger"/> normalizes and merges the two responses. OpenFIGI is preferred for canonical identity
/// (FIGI/ISIN/exchange); Alpha Vantage mainly contributes a provider price-symbol candidate.
/// </summary>
public sealed partial class InvestmentInstrumentDiscoveryService(
    IOpenFigiClient openFigiClient,
    IAlphaVantageClient alphaVantageClient,
    IMemoryCache cache,
    ILogger<InvestmentInstrumentDiscoveryService> logger) : IInvestmentInstrumentDiscoveryService
{
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(15);

    // Pence-style minor currency units that normalise to a major unit via a 0.01 price multiplier.
    // ISIN: 2-letter country code + 9 alphanumerics + 1 check digit.
    [GeneratedRegex("^[A-Za-z]{2}[A-Za-z0-9]{9}[0-9]$")]
    private static partial Regex IsinRegex();

    public async Task<IReadOnlyList<InstrumentDiscoveryResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var normalized = query.Trim();
        var cacheKey = $"INSTRUMENT_DISCOVERY_{normalized.ToUpperInvariant()}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<InstrumentDiscoveryResultDto>? cached) && cached is not null)
            return cached;

        // Provider failures are degraded to empty lists inside SafeOpenFigi/SafeAlphaVantage, so any
        // exception escaping here is a genuine bug (merge/normalize/cache) and should surface rather
        // than masquerade as "no matches"; cancellation propagates naturally.
        var results = IsinRegex().IsMatch(normalized)
            ? await SearchByIsinAsync(normalized.ToUpperInvariant(), ct)
            : await SearchByTickerOrNameAsync(normalized.ToUpperInvariant(), ct);

        cache.Set(cacheKey, results, _cacheTtl);
        return results;
    }

    // ISIN path: OpenFIGI maps an ISIN to all known listing venues. Identity is trusted here, but
    // OpenFIGI supplies neither a currency nor a price symbol, so correlate Alpha Vantage matches
    // for the distinct venue tickers. If Alpha Vantage is unavailable the OpenFIGI-only results are
    // returned unchanged. Alpha Vantage's symbol search is fuzzy, so a match that correlates to no
    // OpenFIGI listing is discarded rather than stamped with the queried ISIN: it may be a different
    // security, and persisting it under this ISIN would corrupt the asset's identity.
    private async Task<IReadOnlyList<InstrumentDiscoveryResultDto>> SearchByIsinAsync(string isin, CancellationToken ct)
    {
        var listings = await SafeOpenFigi(() => openFigiClient.MapByIsinAsync(isin, ct), nameof(SearchByIsinAsync));
        var (results, _) = await BuildListingsAsync(listings, isin, ct);
        return results;
    }

    // Admin draft and "Find listings" requests repeat this lookup for the same share class, and each
    // run can spend up to three Alpha Vantage searches from the shared 25-per-day budget. Only
    // complete lookups are cached: a provider failure degrades to OpenFIGI-only (or empty) results,
    // and caching those would hide the symbols once the provider recovers.
    public async Task<IReadOnlyList<InstrumentDiscoveryResultDto>> DiscoverListingsAsync(string? isin, string? shareClassFigi, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(isin) && string.IsNullOrWhiteSpace(shareClassFigi))
            return [];

        var normalizedIsin = string.IsNullOrWhiteSpace(isin) ? null : isin.Trim().ToUpperInvariant();
        var trimmedShareClassFigi = string.IsNullOrWhiteSpace(shareClassFigi) ? null : shareClassFigi.Trim();
        var cacheKey = $"INSTRUMENT_LISTING_DISCOVERY_{normalizedIsin}_{trimmedShareClassFigi?.ToUpperInvariant()}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<InstrumentDiscoveryResultDto>? cached) && cached is not null)
            return cached;

        var listings = trimmedShareClassFigi is not null
            ? await TryOpenFigi(() => openFigiClient.MapByShareClassFigiAsync(trimmedShareClassFigi, ct), nameof(DiscoverListingsAsync))
            : await TryOpenFigi(() => openFigiClient.MapByIsinAsync(normalizedIsin!, ct), nameof(DiscoverListingsAsync));
        if (listings is null)
            return [];

        if (trimmedShareClassFigi is not null)
            listings = listings.Where(x => string.Equals(x.ShareClassFigi, trimmedShareClassFigi, StringComparison.OrdinalIgnoreCase)).ToList();

        var (results, complete) = await BuildListingsAsync(listings, normalizedIsin, ct);
        if (complete)
            cache.Set(cacheKey, results, _cacheTtl);

        return results;
    }

    // Complete is false when any Alpha Vantage symbol search failed, so the caller can tell
    // degraded OpenFIGI-only results apart from a full lookup.
    private async Task<(IReadOnlyList<InstrumentDiscoveryResultDto> Results, bool Complete)> BuildListingsAsync(IReadOnlyList<OpenFigiListing> listings, string? isin, CancellationToken ct)
    {
        if (listings.Count == 0)
            return ([], true);

        // Alpha Vantage's free tier allows 25 requests/day (shared with price refreshes), so cap
        // the symbol lookups per ISIN search instead of spending one request per venue. Take the
        // first listing's ticker per distinct known venue (in response order), then distinct
        // tickers, capped at three lookups: a security can be listed on one venue under several
        // tickers (London VWRA and VWRP are both returned before Xetra's VWCE), so taking the first
        // N distinct tickers would spend the whole budget on one venue and leave Xetra without a
        // symbol. Unknown venues (GF, LA) cannot be correlated to a listing, so they only
        // contribute tickers when no venue is known at all.
        var candidates = listings
            .Where(listing => !string.IsNullOrWhiteSpace(listing.Ticker))
            .Select(listing => (listing.Ticker, Mapping: BrokerSymbol.TryLookupByVenueToken(listing.ExchCode)))
            .ToList();

        var knownVenueCandidates = candidates.Where(candidate => candidate.Mapping is not null).ToList();
        var tickersToSearch = (knownVenueCandidates.Count > 0 ? knownVenueCandidates : candidates)
            .GroupBy(candidate => candidate.Mapping?.OpenFigiExchCode ?? candidate.Ticker, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().Ticker)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var complete = true;
        var avMatchesByTicker = new Dictionary<string, IReadOnlyList<TickerSearchMatch>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ticker in tickersToSearch)
        {
            var avMatches = await TryAlphaVantage(() => alphaVantageClient.SearchTicker(ticker, ct));
            complete &= avMatches is not null;
            avMatchesByTicker[ticker] = avMatches ?? [];
        }

        var results = new List<InstrumentDiscoveryResultDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in listings.GroupBy(listing => listing.Ticker, StringComparer.OrdinalIgnoreCase))
        {
            var avMatches = avMatchesByTicker.TryGetValue(group.Key, out var matches)
                ? matches
                : [];
            foreach (var dto in InstrumentDiscoveryMerger.Merge(group.Key, avMatches, group.ToList(), isin, includeAlphaVantageOnly: false))
            {
                if (seen.Add(InstrumentDiscoveryMerger.DedupeKey(dto)))
                    results.Add(dto);
            }
        }

        return (results, complete);
    }

    // Ticker/name path: fan out to OpenFIGI ticker mapping and Alpha Vantage symbol search, then merge.
    private async Task<IReadOnlyList<InstrumentDiscoveryResultDto>> SearchByTickerOrNameAsync(string input, CancellationToken ct)
    {
        var brokerSymbol = BrokerSymbol.Parse(input);
        var baseTicker = brokerSymbol.BaseTicker;
        if (string.IsNullOrWhiteSpace(baseTicker))
            return [];

        var isTickerQuery = !baseTicker.Any(char.IsWhiteSpace);
        var hintMapping = brokerSymbol.TryLookupExchange();

        var avTask = SafeAlphaVantage(() => alphaVantageClient.SearchTicker(baseTicker, ct));
        var ofTask = isTickerQuery
            ? MapByTickerWithHintFallbackAsync(baseTicker, hintMapping, ct)
            : Task.FromResult<IReadOnlyList<OpenFigiListing>>([]);
        await Task.WhenAll(avTask, ofTask);

        return InstrumentDiscoveryMerger.Merge(baseTicker, avTask.Result, ofTask.Result);
    }

    // The broker suffix narrows the OpenFIGI query, but OpenFIGI often lists an instrument only on
    // a country composite (VWCE.DE resolves to "GR", never "GY"). When the filtered query is empty,
    // retry unfiltered and keep only the venues that resolve back to the hinted mapping.
    private async Task<IReadOnlyList<OpenFigiListing>> MapByTickerWithHintFallbackAsync(
        string baseTicker,
        BrokerSymbol.ExchangeMapping? hintMapping,
        CancellationToken ct)
    {
        if (hintMapping is null)
            return await SafeOpenFigi(() => openFigiClient.MapByTickerAsync(baseTicker, null, ct), nameof(SearchByTickerOrNameAsync));

        var filtered = await SafeOpenFigi(
            () => openFigiClient.MapByTickerAsync(baseTicker, hintMapping.OpenFigiExchCode, ct),
            nameof(SearchByTickerOrNameAsync));
        if (filtered.Count > 0)
            return filtered;

        var unfiltered = await SafeOpenFigi(
            () => openFigiClient.MapByTickerAsync(baseTicker, null, ct),
            nameof(SearchByTickerOrNameAsync));
        return unfiltered
            .Where(listing => ResolvesToVenue(listing.ExchCode, hintMapping))
            .ToList();
    }

    private static bool ResolvesToVenue(string? exchCode, BrokerSymbol.ExchangeMapping hintMapping) =>
        BrokerSymbol.TryLookupByVenueToken(exchCode) is BrokerSymbol.ExchangeMapping resolved
        && string.Equals(resolved.Mic, hintMapping.Mic, StringComparison.OrdinalIgnoreCase)
        && string.Equals(resolved.ExchangeName, hintMapping.ExchangeName, StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<OpenFigiListing>> SafeOpenFigi(
        Func<Task<IReadOnlyList<OpenFigiListing>>> call,
        string context) =>
        await TryOpenFigi(call, context) ?? [];

    // Returns null when the call fails, so callers can tell a failure from "no listings".
    private async Task<IReadOnlyList<OpenFigiListing>?> TryOpenFigi(
        Func<Task<IReadOnlyList<OpenFigiListing>>> call,
        string context)
    {
        try
        {
            return await call();
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "OpenFIGI call cancelled during {Context}.", context);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "OpenFIGI call failed during {Context}", context);
            return null;
        }
    }

    private async Task<IReadOnlyList<TickerSearchMatch>> SafeAlphaVantage(Func<Task<IReadOnlyList<TickerSearchMatch>>> call) =>
        await TryAlphaVantage(call) ?? [];

    // Returns null when the search fails, so callers can tell a failure from "no matches".
    private async Task<IReadOnlyList<TickerSearchMatch>?> TryAlphaVantage(Func<Task<IReadOnlyList<TickerSearchMatch>>> call)
    {
        try
        {
            return await call();
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Alpha Vantage symbol search cancelled.");
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Alpha Vantage symbol search failed");
            return null;
        }
    }
}