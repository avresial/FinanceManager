using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

public sealed class InstrumentResolver(
    IOpenFigiClient openFigiClient,
    IAlphaVantageClient avClient,
    IStockDetailsRepository stockDetailsRepository,
    IAssetRepository assetRepository,
    IAssetListingRepository assetListingRepository,
    IMarketDataSymbolRepository marketDataSymbolRepository,
    ICurrencyRepository currencyRepository,
    IQuoteFactorResolver quoteFactorResolver,
    IMemoryCache cache,
    ILogger<InstrumentResolver> logger) : IInstrumentResolver
{
    // Pence-style minor currency units that normalise to a major unit via PriceMultiplier 0.01.
    private static readonly HashSet<string> _minorCurrencyUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBX", "GBP.", "ZAC", "ILA",
    };

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

        // Short-circuit: check if already resolved in StockDetails. The stored ticker is the
        // broker-facing alias (e.g. "CSPX.UK"), so try the full input first, then the base ticker.
        var existing = await stockDetailsRepository.GetByTicker(input, ct);
        if (existing is null && !string.Equals(input, baseTicker, StringComparison.OrdinalIgnoreCase))
            existing = await stockDetailsRepository.GetByTicker(baseTicker, ct);
        if (existing?.Isin is not null && existing.AlphaVantageSymbol is not null)
        {
            logger.LogDebug("Found cached StockDetails for {BaseTicker}: ISIN={Isin}, AvSymbol={AvSymbol}", baseTicker, existing.Isin, existing.AlphaVantageSymbol);
            var match = new InstrumentListing(
                existing.Isin,
                existing.AlphaVantageSymbol,
                existing.Name,
                existing.Region,
                existing.Currency.ShortName,
                existing.Type);
            var listingId = await PersistInvestmentModel(match, baseTicker, ct);
            var cached2 = new InstrumentResolution
            {
                Kind = ResolutionKind.AutoResolved,
                Match = listingId is long id ? match with { AssetListingId = id } : match
            };
            cache.Set(cacheKey, cached2, _positiveTtl);
            return cached2;
        }

        // Fan out: AV search + OpenFIGI ticker mapping.
        // Translate the broker suffix to the OpenFIGI exchange code (e.g. "UK" → "LN")
        // before querying OpenFIGI, which keys on its own exchange codes, not broker suffixes.
        var openFigiExchCode = brokerSymbol.TryLookupExchange()?.OpenFigiExchCode;
        var avTask = avClient.SearchTicker(baseTicker, ct);
        var ofTask = OpenFigiCallWithFallback(baseTicker, openFigiExchCode, ct);
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
                await PersistStockDetails(single, input, ct);
                var listingId = await PersistInvestmentModel(single, baseTicker, ct);
                var autoResult = new InstrumentResolution
                {
                    Kind = ResolutionKind.AutoResolved,
                    Match = listingId is long id ? single with { AssetListingId = id } : single
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
            if (exchangeHint is not null && lookupMapping is not null)
            {
                if (!lookupMapping.MatchesRegion(avMatch.Region))
                    continue;
            }

            // Gather all OpenFIGI listings for this base ticker; narrow to the hinted exchange when possible.
            var ofMatches = ofListings
                .Where(x => string.Equals(x.Ticker, baseTicker, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (exchangeHint is not null && lookupMapping is not null)
            {
                var narrowed = ofMatches
                    .Where(x => string.Equals(x.ExchCode, lookupMapping.OpenFigiExchCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (narrowed.Count > 0)
                    ofMatches = narrowed;
            }

            var distinctIsins = ofMatches
                .Where(x => !string.IsNullOrWhiteSpace(x.Isin))
                .Select(x => x.Isin!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctIsins.Count > 1)
            {
                // Ambiguous: the same ticker maps to several distinct instruments and we cannot
                // confidently pick one — emit a candidate per ISIN so the caller disambiguates.
                foreach (var dIsin in distinctIsins)
                {
                    var of = ofMatches.First(x => string.Equals(x.Isin, dIsin, StringComparison.OrdinalIgnoreCase));
                    await AddCandidate(candidates, seen, avMatch, of, ct);
                }
            }
            else
            {
                await AddCandidate(candidates, seen, avMatch, ofMatches.FirstOrDefault(), ct);
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
                        Type: "Unknown",
                        QuoteCurrency: ofListing.Currency));
                }
            }
        }

        return candidates;
    }

    private async Task AddCandidate(
        List<InstrumentListing> candidates,
        HashSet<string> seen,
        TickerSearchMatch avMatch,
        OpenFigiListing? matchingOf,
        CancellationToken ct)
    {
        var avSymbol = avMatch.Symbol;
        var avCurrency = string.IsNullOrWhiteSpace(avMatch.Currency) ? "USD" : avMatch.Currency;

        // When OpenFIGI matched, treat its currency as canonical and convert the AV quote into it.
        // When there is no OpenFIGI match (cooldown/outage/no listing), keep the AV currency as-is.
        string currency;
        decimal quoteFactor;
        if (!string.IsNullOrWhiteSpace(matchingOf?.Currency))
        {
            currency = matchingOf.Currency;
            quoteFactor = await quoteFactorResolver.ResolveAsync(avCurrency, currency, ct);
        }
        else
        {
            currency = avCurrency;
            quoteFactor = 1m;
        }

        var isin = matchingOf?.Isin;
        var candidateKey = $"{isin}_{avSymbol}_{currency}";
        if (seen.Add(candidateKey))
        {
            candidates.Add(new InstrumentListing(
                Isin: isin,
                AlphaVantageSymbol: avSymbol,
                Name: string.IsNullOrWhiteSpace(avMatch.Name) ? matchingOf?.Name ?? string.Empty : avMatch.Name,
                Exchange: matchingOf?.ExchCode ?? avMatch.Region ?? string.Empty,
                Currency: currency,
                Type: string.IsNullOrWhiteSpace(avMatch.Type) ? "Unknown" : avMatch.Type,
                QuoteFactor: quoteFactor,
                QuoteCurrency: avCurrency));
        }
    }

    // Persist the auto-resolved (ISIN, AlphaVantageSymbol) pair so future lookups hit the DB
    // layer instead of fanning out to external providers again. Best-effort: a persistence
    // failure must not fail the resolution itself.
    private async Task PersistStockDetails(InstrumentListing match, string brokerTicker, CancellationToken ct)
    {
        try
        {
            var currency = await currencyRepository.GetOrAdd(match.Currency, match.Currency, ct);
            var details = new StockDetails
            {
                Isin = match.Isin!,
                Ticker = brokerTicker,
                AlphaVantageSymbol = match.AlphaVantageSymbol,
                Name = match.Name,
                Type = match.Type,
                Region = match.Exchange,
                Currency = currency
            };
            await stockDetailsRepository.Add(details, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to persist auto-resolved StockDetails for ISIN {Isin}", match.Isin);
        }
    }

    // Mirror the auto-resolved instrument into the new asset model: upsert Asset (by ISIN) + an ISIN
    // identifier, the concrete AssetListing (Ticker/ExchangeMic/TradingCurrency), and the AlphaVantage
    // MarketDataSymbol. Returns the persisted listing id so callers can attach a transaction to it.
    // Best-effort: a persistence failure must not fail the resolution itself.
    private async Task<long?> PersistInvestmentModel(InstrumentListing match, string baseTicker, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(match.Isin) || string.IsNullOrWhiteSpace(match.AlphaVantageSymbol))
            return null;

        try
        {
            var (mic, exchangeName) = ResolveVenue(match.Exchange);

            // The provider may quote in a minor unit (e.g. GBX pence) that differs from the
            // canonical currency (GBP). Persist the *quote* currency on the listing/symbol and let
            // PriceMultiplier normalise it, rather than storing the canonical currency with a 1x
            // multiplier (which would over-value pence-quoted listings 100x).
            var quoteCurrency = string.IsNullOrWhiteSpace(match.QuoteCurrency) ? match.Currency : match.QuoteCurrency;

            var asset = await assetRepository.Upsert(new Asset
            {
                Name = match.Name,
                Type = MapAssetType(match.Type),
                Isin = match.Isin,
                Identifiers =
                [
                    new AssetIdentifier
                    {
                        Type = AssetIdentifierType.ISIN,
                        Value = match.Isin,
                        Scope = IdentifierScope.Asset,
                        Source = nameof(InstrumentResolver),
                        IsPrimary = true
                    }
                ]
            }, ct);

            var listing = await assetListingRepository.Upsert(new AssetListing
            {
                AssetId = asset.Id,
                Ticker = baseTicker,
                ExchangeMic = mic,
                ExchangeName = exchangeName,
                TradingCurrency = quoteCurrency,
                IsPrimaryListing = true,
                PriceMultiplier = PriceMultiplierFor(quoteCurrency),
                IsActive = true
            }, ct);

            await marketDataSymbolRepository.Upsert(new MarketDataSymbol
            {
                AssetListingId = listing.Id,
                Provider = MarketDataProvider.AlphaVantage,
                Symbol = match.AlphaVantageSymbol,
                Currency = quoteCurrency,
                IsPrimary = true,
                IsEnabled = true
            }, ct);

            return listing.Id;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to persist auto-resolved asset model for ISIN {Isin}", match.Isin);
            return null;
        }
    }

    // Map a reconciled venue token (broker suffix, OpenFIGI exchange code like "LN", or an Alpha
    // Vantage region label like "GB"/"United Kingdom") to an ISO 10383 MIC and human-readable name.
    // Composite venues with no single MIC (e.g. OpenFIGI "US") and unknown tokens fall back to the
    // raw token rather than claiming a specific exchange.
    private static (string Mic, string ExchangeName) ResolveVenue(string? exchange)
    {
        var token = string.IsNullOrWhiteSpace(exchange) ? "UNKNOWN" : exchange.Trim().ToUpperInvariant();

        var mapping = BrokerSymbol.TryLookupByVenueToken(exchange);
        if (mapping is not null)
            return (mapping.Mic ?? token, mapping.ExchangeName);

        return (token, token);
    }

    private static decimal PriceMultiplierFor(string currency) =>
        _minorCurrencyUnits.Contains(currency) ? 0.01m : 1m;

    private static AssetType MapAssetType(string? type) => type?.Trim().ToUpperInvariant() switch
    {
        "ETF" => AssetType.ETF,
        "MUTUAL FUND" or "MUTUALFUND" or "FUND" => AssetType.MutualFund,
        "BOND" => AssetType.Bond,
        "CRYPTO" or "CRYPTOCURRENCY" or "DIGITAL CURRENCY" => AssetType.Crypto,
        "EQUITY" or "STOCK" or "COMMON STOCK" => AssetType.Stock,
        _ => AssetType.Other
    };

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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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