using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

public sealed class InstrumentResolver(
    IOpenFigiClient openFigiClient,
    IAlphaVantageClient avClient,
    IStockDetailsRepository stockDetailsRepository,
    IQuoteFactorResolver quoteFactorResolver,
    IMemoryCache cache,
    ILogger<InstrumentResolver> logger) : IInstrumentResolver
{
    private static readonly TimeSpan _positiveTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan _negativeTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _openFigiFailureCooldown = TimeSpan.FromMinutes(10);
    private DateTime? _openFigiCooldownUntilUtc;

    public async Task<InstrumentResolution> ResolveAsync(string brokerTickerOrTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(brokerTickerOrTerm))
            return NoMatch();

        var input = brokerTickerOrTerm.Trim().ToUpperInvariant();
        var cacheKey = $"INSTRUMENT_RESOLUTION_{input}";

        if (cache.TryGetValue(cacheKey, out InstrumentResolution? cached) && cached is not null)
            return cached;

        var brokerSymbol = BrokerSymbol.Parse(input);
        var baseTicker = brokerSymbol.BaseTicker;

        if (string.IsNullOrWhiteSpace(baseTicker))
            return NoMatch();

        // Short-circuit: check if already resolved in StockDetails
        var existing = await stockDetailsRepository.GetByTicker(baseTicker, ct);
        if (existing?.Isin is not null && existing.AlphaVantageSymbol is not null)
        {
            logger.LogDebug("Found cached StockDetails for {BaseTicker}: ISIN={Isin}, AvSymbol={AvSymbol}", baseTicker, existing.Isin, existing.AlphaVantageSymbol);
            var cached2 = new InstrumentResolution
            {
                Kind = ResolutionKind.AutoResolved,
                Match = new InstrumentListing(
                    existing.Isin,
                    existing.AlphaVantageSymbol,
                    existing.Name,
                    existing.Region,
                    existing.Currency.ShortName,
                    existing.Type)
            };
            cache.Set(cacheKey, cached2, _positiveTtl);
            return cached2;
        }

        // Fan out: AV search + OpenFIGI ticker mapping
        var avTask = avClient.SearchTicker(baseTicker, ct);
        var ofTask = OpenFigiCallWithFallback(baseTicker, brokerSymbol.ExchangeHint, ct);
        await Task.WhenAll(avTask, ofTask);

        var avMatches = avTask.Result;
        var ofListings = ofTask.Result;

        // Reconcile: pair AV symbol/currency/region against OpenFIGI ISIN/venue
        var candidates = await Reconcile(baseTicker, brokerSymbol.ExchangeHint, avMatches, ofListings, ct);

        if (candidates.Count == 0)
        {
            var result = NoMatch();
            cache.Set(cacheKey, result, _negativeTtl);
            return result;
        }

        if (candidates.Count == 1)
        {
            var single = candidates[0];
            if (single.Isin is not null && single.AlphaVantageSymbol is not null)
            {
                logger.LogDebug("Auto-resolving single match: ISIN={Isin}, AvSymbol={AvSymbol}", single.Isin, single.AlphaVantageSymbol);
                var autoResult = new InstrumentResolution
                {
                    Kind = ResolutionKind.AutoResolved,
                    Match = single
                };
                cache.Set(cacheKey, autoResult, _positiveTtl);
                return autoResult;
            }
        }

        var ambiguousResult = new InstrumentResolution
        {
            Kind = ResolutionKind.Ambiguous,
            Candidates = candidates
        };
        cache.Set(cacheKey, ambiguousResult, _negativeTtl);
        return ambiguousResult;
    }

    private async Task<List<InstrumentListing>> Reconcile(
        string baseTicker,
        string? exchangeHint,
        IReadOnlyList<TickerSearchMatch> avMatches,
        IReadOnlyList<OpenFigiListing> ofListings,
        CancellationToken ct)
    {
        var candidates = new List<InstrumentListing>();
        var seen = new HashSet<string>();

        var brokerSymbolInput = exchangeHint is not null ? $"{baseTicker}.{exchangeHint}" : baseTicker;
        var lookupMapping = BrokerSymbol.Parse(brokerSymbolInput).TryLookupExchange();

        // For each AV match, reconcile with OpenFIGI data
        foreach (var avMatch in avMatches)
        {
            if (string.IsNullOrWhiteSpace(avMatch.Symbol))
                continue;

            // Filter by exchange hint if provided
            if (exchangeHint is not null && lookupMapping.HasValue)
            {
                var (_, expectedAvRegion) = lookupMapping.Value;
                if (!string.Equals(avMatch.Region, expectedAvRegion, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Extract base ticker from AV symbol (e.g., "CSPX.LON" → "CSPX")
            var avBaseTicker = avMatch.Symbol.Split('.')[0];

            // Find matching OpenFIGI listing: match by base ticker or by exchange hint
            OpenFigiListing? matchingOf = null;

            if (exchangeHint is not null && lookupMapping.HasValue)
            {
                // If we have an exchange hint, find OF listing with matching exchange
                var (ofExchCode, _) = lookupMapping.Value;
                matchingOf = ofListings.FirstOrDefault(x =>
                    string.Equals(x.Ticker, baseTicker, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.ExchCode, ofExchCode, StringComparison.OrdinalIgnoreCase));
            }

            if (matchingOf is null)
            {
                // Fall back to matching by base ticker (any exchange)
                matchingOf = ofListings.FirstOrDefault(x =>
                    string.Equals(x.Ticker, baseTicker, StringComparison.OrdinalIgnoreCase));
            }

            var isin = matchingOf?.Isin;
            var avSymbol = avMatch.Symbol;

            var avCurrency = avMatch.Currency ?? "USD";
            var ofCurrency = matchingOf?.Currency ?? "USD";
            var quoteFactor = await quoteFactorResolver.ResolveAsync(avCurrency, ofCurrency, ct);
            var currency = ofCurrency;  // Use OF currency as the canonical one after conversion

            var candidateKey = $"{isin}_{avSymbol}_{currency}";
            if (seen.Add(candidateKey))
            {
                candidates.Add(new InstrumentListing(
                    Isin: isin,
                    AlphaVantageSymbol: avSymbol,
                    Name: avMatch.Name ?? matchingOf?.Name ?? string.Empty,
                    Exchange: matchingOf?.ExchCode ?? avMatch.Region ?? string.Empty,
                    Currency: currency,
                    Type: avMatch.Type ?? matchingOf?.Name ?? "Unknown",
                    QuoteFactor: quoteFactor));
            }
        }

        // If no AV matches, include AV-less OpenFIGI listings (for confirmation-only, won't auto-persist)
        if (candidates.Count == 0)
        {
            foreach (var ofListing in ofListings.Where(x => x.Isin is not null).DistinctBy(x => x.Isin))
            {
                var candidateKey = $"{ofListing.Isin}___{ofListing.Currency ?? "USD"}";
                if (seen.Add(candidateKey))
                {
                    candidates.Add(new InstrumentListing(
                        Isin: ofListing.Isin,
                        AlphaVantageSymbol: null,
                        Name: ofListing.Name,
                        Exchange: ofListing.ExchCode,
                        Currency: ofListing.Currency ?? "USD",
                        Type: "Unknown"));
                }
            }
        }

        return candidates;
    }

    private async Task<IReadOnlyList<OpenFigiListing>> OpenFigiCallWithFallback(string baseTicker, string? exchCode, CancellationToken ct)
    {
        if (IsInOpenFigiCooldown())
        {
            logger.LogDebug("OpenFIGI in failure cooldown; skipping call");
            return [];
        }

        try
        {
            var results = await openFigiClient.MapByTickerAsync(baseTicker, exchCode, ct);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenFIGI call failed for {BaseTicker}; entering cooldown", baseTicker);
            _openFigiCooldownUntilUtc = DateTime.UtcNow.Add(_openFigiFailureCooldown);
            return [];
        }
    }

    private bool IsInOpenFigiCooldown()
    {
        var until = _openFigiCooldownUntilUtc;
        return until.HasValue && DateTime.UtcNow < until.Value;
    }

    private static InstrumentResolution NoMatch() => new()
    {
        Kind = ResolutionKind.NoMatch,
        Candidates = []
    };
}