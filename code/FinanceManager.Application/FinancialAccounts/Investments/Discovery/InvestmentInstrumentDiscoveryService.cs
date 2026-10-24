using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

/// <summary>
/// Searches OpenFIGI and Alpha Vantage and normalizes/merges their responses into listing-level
/// <see cref="InstrumentDiscoveryResultDto"/>s. OpenFIGI is preferred for canonical identity
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
        if (listings.Count == 0)
            return [];

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

        var avMatchesByTicker = new Dictionary<string, IReadOnlyList<TickerSearchMatch>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ticker in tickersToSearch)
            avMatchesByTicker[ticker] = await SafeAlphaVantage(() => alphaVantageClient.SearchTicker(ticker, ct));

        var results = new List<InstrumentDiscoveryResultDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in listings.GroupBy(listing => listing.Ticker, StringComparer.OrdinalIgnoreCase))
        {
            var avMatches = avMatchesByTicker.TryGetValue(group.Key, out var matches)
                ? matches
                : [];
            foreach (var dto in Merge(group.Key, avMatches, group.ToList(), isin, includeAlphaVantageOnly: false))
            {
                if (seen.Add(DedupeKey(dto)))
                    results.Add(dto);
            }
        }

        return results;
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

        return Merge(baseTicker, avTask.Result, ofTask.Result);
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

    // Prefer OpenFIGI listings as the spine (canonical identity + venue), attaching an Alpha Vantage
    // symbol when one can be correlated by exchange region. On the ticker/name path, Alpha Vantage
    // matches that map to no OpenFIGI listing are emitted as lower-confidence, AV-only results; the
    // ISIN path suppresses them because it cannot trust their identity.
    private List<InstrumentDiscoveryResultDto> Merge(
        string baseTicker,
        IReadOnlyList<TickerSearchMatch> avMatches,
        IReadOnlyList<OpenFigiListing> ofListings,
        string? isinOverride = null,
        bool includeAlphaVantageOnly = true)
    {
        var results = new List<InstrumentDiscoveryResultDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var consumedAvSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var relevantOf = CollapseCompositeVenues(ofListings)
            .Where(x => string.Equals(x.Ticker, baseTicker, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var of in relevantOf)
        {
            var av = FindMatchingAv(of, avMatches, consumedAvSymbols);
            if (av is not null)
                consumedAvSymbols.Add(av.Symbol);

            var dto = BuildFromOpenFigi(of, av?.Symbol, av?.Currency, av?.Type, isinOverride);
            var key = DedupeKey(dto);
            if (seen.Add(key))
                results.Add(dto);
        }

        if (!includeAlphaVantageOnly)
            return results;

        foreach (var av in avMatches)
        {
            if (string.IsNullOrWhiteSpace(av.Symbol) || consumedAvSymbols.Contains(av.Symbol))
                continue;

            var dto = BuildFromAlphaVantage(av);
            var key = DedupeKey(dto);
            if (seen.Add(key))
                results.Add(dto);
        }

        return results;
    }

    // Drop a country-composite listing (e.g. GR for Germany) when the same ticker is also listed on
    // that venue's primary OpenFIGI code (GY). Otherwise the composite listing stands in for the
    // venue, resolving to the same MIC and exchange name.
    private static IReadOnlyList<OpenFigiListing> CollapseCompositeVenues(IReadOnlyList<OpenFigiListing> listings)
    {
        if (!listings.Any(listing => BrokerSymbol.IsComposite(listing.ExchCode)))
            return listings;

        return listings
            .Where(listing =>
            {
                if (!BrokerSymbol.IsComposite(listing.ExchCode))
                    return true;

                var mapping = BrokerSymbol.TryLookupByVenueToken(listing.ExchCode);
                return mapping is null || !listings.Any(other =>
                    !ReferenceEquals(other, listing)
                    && string.Equals(other.ExchCode, mapping.OpenFigiExchCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(other.Ticker, listing.Ticker, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();
    }

    // Correlate an Alpha Vantage match to an OpenFIGI listing by venue region and ticker, so the AV
    // provider symbol is attached to the right security and venue rather than an arbitrary one.
    // OpenFIGI has no currency, so currency cannot be used for correlation. The ticker check is
    // required because AV's symbol search is fuzzy: a search for "ABC" can return "ABCD.DEX", whose
    // prices belong to a different security. Symbols already attached to an earlier listing are
    // skipped so one AV symbol never spans multiple listings.
    private static TickerSearchMatch? FindMatchingAv(
        OpenFigiListing of,
        IReadOnlyList<TickerSearchMatch> avMatches,
        HashSet<string> consumedAvSymbols)
    {
        var mapping = BrokerSymbol.TryLookupByVenueToken(of.ExchCode);
        if (mapping is null)
            return null;

        foreach (var av in avMatches)
        {
            if (string.IsNullOrWhiteSpace(av.Symbol) || consumedAvSymbols.Contains(av.Symbol))
                continue;

            if (mapping.MatchesRegion(av.Region)
                && string.Equals(ProviderBaseTicker(av.Symbol), of.Ticker, StringComparison.OrdinalIgnoreCase))
                return av;
        }

        return null;
    }

    // Alpha Vantage suffixes its symbols with the exchange ("VWCE.DEX", "VWCE.FRK"); the base
    // ticker is the part before the last separator. Symbols without a suffix (for example US
    // tickers) are returned unchanged.
    private static string ProviderBaseTicker(string symbol)
    {
        var lastDot = symbol.LastIndexOf('.');
        return lastDot > 0 ? symbol[..lastDot] : symbol;
    }

    private static InstrumentDiscoveryResultDto BuildFromOpenFigi(
        OpenFigiListing of,
        string? providerSymbol,
        string? avQuoteCurrency,
        string? avSecurityType,
        string? isinOverride)
    {
        var (mic, exchangeName, exchangeCode) = ResolveVenue(of.ExchCode);
        var mapping = BrokerSymbol.TryLookupByVenueToken(of.ExchCode);
        var currency = FirstNonBlank(avQuoteCurrency, mapping?.DefaultCurrency);
        var warnings = new List<string>();

        if (mic is null)
            warnings.Add($"Exchange mapping for code '{exchangeCode}' is ambiguous or unknown; confirm the market (MIC) before importing.");

        if (InstrumentCurrencyUnits.IsMinor(currency))
            warnings.Add($"Listing is quoted in minor units ({currency}); a price multiplier of 0.01 will be applied to convert to the major unit.");

        if (providerSymbol is null)
            warnings.Add("No Alpha Vantage price symbol was found for this listing; automatic price updates may be unavailable.");

        var source = providerSymbol is null ? "OpenFIGI" : "Combined";
        var confidence = providerSymbol is null ? 0.7m : 1.0m;

        return new InstrumentDiscoveryResultDto
        {
            Source = source,
            DisplayName = of.Name,
            Ticker = of.Ticker,
            ExchangeMic = mic,
            ExchangeCode = exchangeCode,
            ExchangeName = exchangeName,
            TradingCurrency = currency,
            ListingFigi = of.Figi,
            ShareClassFigi = of.ShareClassFigi,
            CompositeFigi = of.CompositeFigi,
            Isin = isinOverride ?? of.Isin,
            SecurityType = FirstNonBlank(avSecurityType, of.SecurityType, of.SecurityType2),
            MarketSector = of.MarketSector,
            ProviderSymbol = providerSymbol,
            ConfidenceScore = confidence,
            Warnings = warnings,
        };
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static InstrumentDiscoveryResultDto BuildFromAlphaVantage(TickerSearchMatch av)
    {
        // Normalise the AV-only result: derive canonical venue identity from the AV region (its
        // aliases are understood by BrokerSymbol) and the display ticker from the AV symbol's base
        // ticker, because the search text may be a name (e.g. "TESCO" returning "TSCO.LON"). The
        // raw provider symbol is preserved separately for price fetching. The result carries no
        // ISIN/FIGI because Alpha Vantage does not confirm identity.
        var (mic, exchangeName, exchangeCode) = ResolveVenue(av.Region);
        var warnings = new List<string>
        {
            "Identity was not confirmed by OpenFIGI; check the exchange and currency before saving.",
        };

        return new InstrumentDiscoveryResultDto
        {
            Source = "AlphaVantage",
            DisplayName = av.Name,
            Ticker = ProviderBaseTicker(av.Symbol),
            ExchangeMic = mic,
            ExchangeName = exchangeName,
            ExchangeCode = exchangeCode,
            TradingCurrency = string.IsNullOrWhiteSpace(av.Currency) ? null : av.Currency,
            SecurityType = string.IsNullOrWhiteSpace(av.Type) ? null : av.Type,
            ProviderSymbol = av.Symbol,
            // AV-only results carry no canonical identity, so cap confidence below any corroborated result.
            ConfidenceScore = Math.Clamp(av.MatchScore, 0m, 0.6m),
            Warnings = warnings,
        };
    }

    // Map an OpenFIGI exchange code to (MIC, exchange name, code). A null MIC signals an unknown or
    // composite venue (e.g. OpenFIGI "US") that the caller must confirm.
    private static (string? Mic, string ExchangeName, string ExchangeCode) ResolveVenue(string? exchCode)
    {
        var token = string.IsNullOrWhiteSpace(exchCode) ? "UNKNOWN" : exchCode.Trim().ToUpperInvariant();
        var mapping = BrokerSymbol.TryLookupByVenueToken(exchCode);
        return mapping is not null
            ? (mapping.Mic, mapping.ExchangeName, token)
            : (null, token, token);
    }

    // Identity-first dedupe: prefer FIGI, then ISIN+currency, then ticker+exchange+currency.
    private static string DedupeKey(InstrumentDiscoveryResultDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.ListingFigi))
            return $"FIGI:{dto.ListingFigi}";
        if (!string.IsNullOrWhiteSpace(dto.ShareClassFigi))
            return $"SCFIGI:{dto.ShareClassFigi}:{dto.TradingCurrency}";
        if (!string.IsNullOrWhiteSpace(dto.Isin))
            return $"ISIN:{dto.Isin}:{dto.TradingCurrency}";
        return $"TICK:{dto.Ticker}:{dto.ExchangeCode}:{dto.TradingCurrency}";
    }

    private async Task<IReadOnlyList<OpenFigiListing>> SafeOpenFigi(
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
            return [];
        }
    }

    private async Task<IReadOnlyList<TickerSearchMatch>> SafeAlphaVantage(Func<Task<IReadOnlyList<TickerSearchMatch>>> call)
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
            return [];
        }
    }
}