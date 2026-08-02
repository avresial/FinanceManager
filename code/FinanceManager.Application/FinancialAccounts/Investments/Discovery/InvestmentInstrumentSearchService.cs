using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public sealed class InvestmentInstrumentSearchService(
    IAssetListingRepository listingRepository,
    IInvestmentInstrumentDiscoveryService discoveryService,
    IMemoryCache cache) : IInvestmentInstrumentSearchService
{
    private static readonly TimeSpan _resultTtl = TimeSpan.FromMinutes(15);
    private const string _cachePrefix = "INVESTMENT_INSTRUMENT_RESULT_";

    public async Task<IReadOnlyList<InvestmentInstrumentOptionDto>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
            return [];

        maxResults = Math.Clamp(maxResults, 1, 50);
        var localListings = await listingRepository.SearchAsync(query, maxResults, cancellationToken);
        var results = localListings.Select(ToExistingOption).ToList();
        if (results.Count >= maxResults)
            return results;

        var externalResults = await discoveryService.SearchAsync(query, cancellationToken);
        foreach (var external in externalResults.OrderByDescending(x => x.ConfidenceScore))
        {
            if (localListings.Any(listing => MatchesExisting(listing, external)))
                continue;

            var resultId = Guid.NewGuid().ToString("N");
            cache.Set(CacheKey(resultId), external, _resultTtl);
            results.Add(new InvestmentInstrumentOptionDto
            {
                ResultId = resultId,
                Source = InstrumentOptionSource.External,
                ExternalInstrument = external,
                Ticker = external.Ticker,
                DisplayName = external.DisplayName,
                ExchangeMic = external.ExchangeMic,
                ExchangeName = external.ExchangeName ?? external.ExchangeMic ?? string.Empty,
                TradingCurrency = external.TradingCurrency ?? string.Empty,
                Isin = external.Isin,
                Warnings = external.Warnings
            });

            if (results.Count == maxResults)
                break;
        }

        return results;
    }

    public Task<InstrumentDiscoveryResultDto?> ResolveExternalResultAsync(
        string resultId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(resultId)
            || !cache.TryGetValue(CacheKey(resultId), out InstrumentDiscoveryResultDto? instrument))
            return Task.FromResult<InstrumentDiscoveryResultDto?>(null);

        return Task.FromResult(instrument);
    }

    private static InvestmentInstrumentOptionDto ToExistingOption(AssetListing listing) => new()
    {
        ResultId = $"listing:{listing.Id}",
        Source = InstrumentOptionSource.Existing,
        AssetListingId = listing.Id,
        Ticker = listing.Ticker,
        DisplayName = listing.Asset?.Name ?? listing.Ticker,
        ExchangeMic = listing.ExchangeMic,
        ExchangeName = listing.ExchangeName,
        TradingCurrency = listing.TradingCurrency,
        Isin = listing.Asset?.Isin
    };

    private static bool MatchesExisting(AssetListing listing, InstrumentDiscoveryResultDto external)
    {
        if (!string.IsNullOrWhiteSpace(external.ListingFigi)
            && string.Equals(listing.ListingFigi, external.ListingFigi, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(external.ProviderSymbol)
            && listing.MarketDataSymbols.Any(symbol =>
                symbol.Provider == MarketDataProvider.AlphaVantage
                && string.Equals(symbol.Symbol, external.ProviderSymbol, StringComparison.OrdinalIgnoreCase)))
            return true;

        return string.Equals(listing.Ticker, external.Ticker, StringComparison.OrdinalIgnoreCase)
            && string.Equals(listing.ExchangeMic, external.ExchangeMic, StringComparison.OrdinalIgnoreCase)
            && string.Equals(listing.TradingCurrency, external.TradingCurrency, StringComparison.OrdinalIgnoreCase);
    }

    private static string CacheKey(string resultId) => $"{_cachePrefix}{resultId}";
}