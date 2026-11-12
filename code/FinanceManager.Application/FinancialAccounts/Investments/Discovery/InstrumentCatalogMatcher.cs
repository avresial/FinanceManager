using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

internal sealed class InstrumentCatalogMatcher(
    IAssetRepository assetRepository,
    IAssetListingRepository listingRepository,
    IMarketDataSymbolRepository symbolRepository)
{
    public async Task<(Asset? Asset, AssetListing? Listing, MarketDataSymbol? Symbol)> FindExistingAsync(
        InstrumentDiscoveryResultDto instrument,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(instrument.ProviderSymbol))
        {
            var providerSymbol = await symbolRepository.Get(MarketDataProvider.AlphaVantage, instrument.ProviderSymbol, ct);
            if (providerSymbol is not null)
            {
                var providerListing = await listingRepository.Get(providerSymbol.AssetListingId, ct);
                if (providerListing is not null)
                    return (await assetRepository.Get(providerListing.AssetId, ct), providerListing, providerSymbol);
            }
        }

        Asset? asset = null;
        if (!string.IsNullOrWhiteSpace(instrument.ShareClassFigi))
            asset = await assetRepository.GetByShareClassFigi(instrument.ShareClassFigi, ct);
        if (asset is null && !string.IsNullOrWhiteSpace(instrument.CompositeFigi))
            asset = (await assetRepository.GetAll(ct)).FirstOrDefault(x => x.CompositeFigi == instrument.CompositeFigi);
        if (asset is null && !string.IsNullOrWhiteSpace(instrument.Isin))
            asset = await assetRepository.GetByIsin(instrument.Isin, ct);
        if (asset is null)
            asset = (await assetRepository.GetAll(ct)).FirstOrDefault(x =>
                x.Type == InstrumentImportService.ParseAssetType(instrument.SecurityType)
                && string.Equals(x.Name.Trim(), instrument.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase));

        AssetListing? listing = null;
        var expectedMic = NormalizeOptionalUpper(instrument.ExchangeMic);
        var expectedCurrency = NormalizeOptionalUpper(instrument.TradingCurrency);
        if (expectedMic is not null && expectedCurrency is not null)
        {
            listing = await listingRepository.Get(instrument.Ticker.Trim().ToUpperInvariant(), expectedMic, expectedCurrency, ct);
            if (listing is not null)
                asset = await assetRepository.Get(listing.AssetId, ct);
        }
        if (asset is not null)
        {
            var listings = await listingRepository.GetByAsset(asset.Id, ct);
            if (!string.IsNullOrWhiteSpace(instrument.ListingFigi))
                listing = listings.FirstOrDefault(x => string.Equals(x.ListingFigi, instrument.ListingFigi, StringComparison.OrdinalIgnoreCase));
            var expectedExchangeName = NormalizeOptional(instrument.ExchangeName)
                ?? expectedMic
                ?? NormalizeOptional(instrument.ExchangeCode)
                ?? string.Empty;
            listing ??= listings.FirstOrDefault(x =>
                string.Equals(x.Ticker, instrument.Ticker, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ExchangeMic, expectedMic, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.TradingCurrency, expectedCurrency, StringComparison.OrdinalIgnoreCase)
                && (expectedMic is not null && expectedCurrency is not null
                    || string.Equals(x.ExchangeName, expectedExchangeName, StringComparison.OrdinalIgnoreCase)));
        }

        MarketDataSymbol? symbol = null;
        if (listing is not null && !string.IsNullOrWhiteSpace(instrument.ProviderSymbol))
            symbol = (await symbolRepository.GetByListing(listing.Id, ct)).FirstOrDefault(x =>
                x.Provider == MarketDataProvider.AlphaVantage && x.Symbol.Equals(instrument.ProviderSymbol, StringComparison.OrdinalIgnoreCase));
        return (asset, listing, symbol);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptionalUpper(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}