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
    private static readonly HashSet<string> _minorCurrencyUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBX", "GBP.", "ZAC", "ILA",
    };

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

    // ISIN path: OpenFIGI maps an ISIN to all known listing venues. Identity is fully trusted here;
    // there is no Alpha Vantage symbol yet, so each result is flagged as missing a price symbol.
    private async Task<IReadOnlyList<InstrumentDiscoveryResultDto>> SearchByIsinAsync(string isin, CancellationToken ct)
    {
        var listings = await SafeOpenFigi(() => openFigiClient.MapByIsinAsync(isin, ct), nameof(SearchByIsinAsync));

        return listings
            .Select(of => BuildFromOpenFigi(of, providerSymbol: null, avQuoteCurrency: null, isinOverride: isin))
            .ToList();
    }

    // Ticker/name path: fan out to OpenFIGI ticker mapping and Alpha Vantage symbol search, then merge.
    private async Task<IReadOnlyList<InstrumentDiscoveryResultDto>> SearchByTickerOrNameAsync(string input, CancellationToken ct)
    {
        var brokerSymbol = BrokerSymbol.Parse(input);
        var baseTicker = brokerSymbol.BaseTicker;
        if (string.IsNullOrWhiteSpace(baseTicker))
            return [];

        var openFigiExchCode = brokerSymbol.TryLookupExchange()?.OpenFigiExchCode;

        var avTask = SafeAlphaVantage(() => alphaVantageClient.SearchTicker(baseTicker, ct));
        var ofTask = SafeOpenFigi(() => openFigiClient.MapByTickerAsync(baseTicker, openFigiExchCode, ct), nameof(SearchByTickerOrNameAsync));
        await Task.WhenAll(avTask, ofTask);

        return Merge(baseTicker, avTask.Result, ofTask.Result);
    }

    // Prefer OpenFIGI listings as the spine (canonical identity + venue), attaching an Alpha Vantage
    // symbol when one can be correlated by exchange/currency. Alpha Vantage matches that map to no
    // OpenFIGI listing are emitted as lower-confidence, AV-only results.
    private List<InstrumentDiscoveryResultDto> Merge(
        string baseTicker,
        IReadOnlyList<TickerSearchMatch> avMatches,
        IReadOnlyList<OpenFigiListing> ofListings)
    {
        var results = new List<InstrumentDiscoveryResultDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var consumedAvSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var relevantOf = ofListings
            .Where(x => string.Equals(x.Ticker, baseTicker, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var of in relevantOf)
        {
            var av = FindMatchingAv(of, avMatches, consumedAvSymbols);
            if (av is not null)
                consumedAvSymbols.Add(av.Symbol);

            var dto = BuildFromOpenFigi(of, av?.Symbol, av?.Currency, isinOverride: null);
            var key = DedupeKey(dto);
            if (seen.Add(key))
                results.Add(dto);
        }

        foreach (var av in avMatches)
        {
            if (string.IsNullOrWhiteSpace(av.Symbol) || consumedAvSymbols.Contains(av.Symbol))
                continue;

            var dto = BuildFromAlphaVantage(baseTicker, av);
            var key = DedupeKey(dto);
            if (seen.Add(key))
                results.Add(dto);
        }

        return results;
    }

    // Correlate an Alpha Vantage match to an OpenFIGI listing by exchange-region or currency, so the
    // AV provider symbol is attached to the right venue rather than an arbitrary one. Symbols already
    // attached to an earlier listing are skipped so one AV symbol never spans multiple listings.
    private static TickerSearchMatch? FindMatchingAv(
        OpenFigiListing of,
        IReadOnlyList<TickerSearchMatch> avMatches,
        HashSet<string> consumedAvSymbols)
    {
        var mapping = BrokerSymbol.TryLookupByVenueToken(of.ExchCode);

        foreach (var av in avMatches)
        {
            if (string.IsNullOrWhiteSpace(av.Symbol) || consumedAvSymbols.Contains(av.Symbol))
                continue;

            var regionMatch = mapping is not null && mapping.MatchesRegion(av.Region);
            var currencyMatch = !string.IsNullOrWhiteSpace(of.Currency)
                && string.Equals(of.Currency, av.Currency, StringComparison.OrdinalIgnoreCase);

            if (regionMatch || currencyMatch)
                return av;
        }

        return null;
    }

    private static InstrumentDiscoveryResultDto BuildFromOpenFigi(
        OpenFigiListing of,
        string? providerSymbol,
        string? avQuoteCurrency,
        string? isinOverride)
    {
        var (mic, exchangeName, exchangeCode) = ResolveVenue(of.ExchCode);
        var currency = of.Currency;
        var warnings = new List<string>();

        if (mic is null)
            warnings.Add($"Exchange mapping for code '{exchangeCode}' is ambiguous or unknown; confirm the market (MIC) before importing.");

        if (!string.IsNullOrWhiteSpace(currency) && _minorCurrencyUnits.Contains(currency))
            warnings.Add($"Listing is quoted in minor units ({currency}); a price multiplier of 0.01 will be applied to convert to the major unit.");

        if (providerSymbol is null)
            warnings.Add("No Alpha Vantage price symbol was found for this listing; automatic price updates may be unavailable.");
        else if (!string.IsNullOrWhiteSpace(avQuoteCurrency) && !string.IsNullOrWhiteSpace(currency)
                 && !string.Equals(avQuoteCurrency, currency, StringComparison.OrdinalIgnoreCase)
                 && !_minorCurrencyUnits.Contains(currency))
            warnings.Add($"Alpha Vantage quote currency ({avQuoteCurrency}) differs from the listing trading currency ({currency}); confirm before importing.");

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
            ProviderSymbol = providerSymbol,
            ConfidenceScore = confidence,
            Warnings = warnings,
        };
    }

    private static InstrumentDiscoveryResultDto BuildFromAlphaVantage(string baseTicker, TickerSearchMatch av)
    {
        // Normalise the AV-only result: derive canonical venue identity from the AV region (its
        // aliases are understood by BrokerSymbol), keep the base ticker as the display ticker, and
        // preserve the raw provider symbol (e.g. "CSPX.LON") separately for price fetching.
        var (mic, exchangeName, exchangeCode) = ResolveVenue(av.Region);
        var warnings = new List<string>
        {
            "Identity was not confirmed by OpenFIGI; verify the ISIN/FIGI and exchange before importing.",
        };

        return new InstrumentDiscoveryResultDto
        {
            Source = "AlphaVantage",
            DisplayName = av.Name,
            Ticker = string.IsNullOrWhiteSpace(baseTicker) ? av.Symbol : baseTicker,
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Alpha Vantage symbol search failed");
            return [];
        }
    }
}