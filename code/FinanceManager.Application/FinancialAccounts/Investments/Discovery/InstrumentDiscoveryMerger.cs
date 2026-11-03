using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

/// <summary>
/// Normalizes OpenFIGI listings and Alpha Vantage symbol matches into
/// <see cref="InstrumentDiscoveryResultDto"/>s and merges them. OpenFIGI is preferred for canonical
/// identity (FIGI/ISIN/exchange); Alpha Vantage mainly contributes a provider price-symbol candidate.
/// Pure: no provider calls.
/// </summary>
internal static class InstrumentDiscoveryMerger
{
    // Prefer OpenFIGI listings as the spine (canonical identity + venue), attaching an Alpha Vantage
    // symbol when one can be correlated by exchange region. On the ticker/name path, Alpha Vantage
    // matches that map to no OpenFIGI listing are emitted as lower-confidence, AV-only results; the
    // ISIN path suppresses them because it cannot trust their identity.
    public static List<InstrumentDiscoveryResultDto> Merge(
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
    public static string DedupeKey(InstrumentDiscoveryResultDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.ListingFigi))
            return $"FIGI:{dto.ListingFigi}";
        if (!string.IsNullOrWhiteSpace(dto.ShareClassFigi))
            return $"SCFIGI:{dto.ShareClassFigi}:{dto.TradingCurrency}";
        if (!string.IsNullOrWhiteSpace(dto.Isin))
            return $"ISIN:{dto.Isin}:{dto.TradingCurrency}";
        return $"TICK:{dto.Ticker}:{dto.ExchangeCode}:{dto.TradingCurrency}";
    }
}