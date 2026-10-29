using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public sealed class InvestmentInstrumentSearchService(
    IAssetListingRepository listingRepository,
    IInvestmentInstrumentDiscoveryService discoveryService,
    IInstrumentImportService importService,
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
            // Hide results that can never be saved: they only lead the user to an error on save.
            if (!importService.CanImportForTransaction(external))
                continue;

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
                ExchangeName = external.ExchangeName ?? external.ExchangeMic ?? external.ExchangeCode ?? string.Empty,
                TradingCurrency = external.TradingCurrency ?? string.Empty,
                Isin = external.Isin,
                Warnings = BuildExternalWarnings(external)
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
        TradingCurrency = listing.TradingCurrency ?? string.Empty,
        Isin = listing.Asset?.Isin,
        Warnings = BuildExistingWarnings(listing)
    };

    private static IReadOnlyList<string> BuildExistingWarnings(AssetListing listing)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(listing.ExchangeMic))
            warnings.Add("Exchange venue is not confirmed yet.");
        if (string.IsNullOrWhiteSpace(listing.TradingCurrency))
            warnings.Add("Trading currency is unknown; choose the transaction currency.");
        if (!listing.HasEnabledSymbol)
            warnings.Add("Automatic prices aren't available yet.");
        return warnings;
    }

    private static IReadOnlyList<string> BuildExternalWarnings(InstrumentDiscoveryResultDto external)
    {
        var warnings = external.Warnings.ToList();
        if (string.IsNullOrWhiteSpace(external.ExchangeMic)
            && !warnings.Any(w => w.Contains("venue", StringComparison.OrdinalIgnoreCase) || w.Contains("exchange", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("Exchange venue could not be mapped automatically; an administrator can complete it later.");
        }

        if (string.IsNullOrWhiteSpace(external.TradingCurrency)
            && !warnings.Any(w => w.Contains("currency", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("Trading currency was not provided; select the currency for this transaction.");
        }

        return warnings;
    }

    private static bool MatchesExisting(AssetListing listing, InstrumentDiscoveryResultDto external)
    {
        if (!string.IsNullOrWhiteSpace(external.ListingFigi)
            && string.Equals(listing.ListingFigi, external.ListingFigi, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(external.ListingFigi) && !string.IsNullOrWhiteSpace(listing.ListingFigi))
            return false;

        if (!string.IsNullOrWhiteSpace(external.ProviderSymbol)
            && listing.MarketDataSymbols.Any(symbol =>
                symbol.Provider == MarketDataProvider.AlphaVantage
                && string.Equals(symbol.Symbol, external.ProviderSymbol, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!string.IsNullOrWhiteSpace(external.ExchangeMic) && !string.IsNullOrWhiteSpace(external.TradingCurrency))
        {
            return string.Equals(listing.Ticker, external.Ticker, StringComparison.OrdinalIgnoreCase)
                && string.Equals(listing.ExchangeMic, external.ExchangeMic, StringComparison.OrdinalIgnoreCase)
                && string.Equals(listing.TradingCurrency, external.TradingCurrency, StringComparison.OrdinalIgnoreCase);
        }

        var expectedExchangeName = external.ExchangeName ?? external.ExchangeMic ?? external.ExchangeCode ?? string.Empty;
        return string.Equals(listing.Ticker, external.Ticker, StringComparison.OrdinalIgnoreCase)
            && string.Equals(listing.ExchangeMic, external.ExchangeMic, StringComparison.OrdinalIgnoreCase)
            && string.Equals(listing.TradingCurrency, external.TradingCurrency, StringComparison.OrdinalIgnoreCase)
            && string.Equals(listing.ExchangeName, expectedExchangeName, StringComparison.OrdinalIgnoreCase);
    }

    private static string CacheKey(string resultId) => $"{_cachePrefix}{resultId}";
}