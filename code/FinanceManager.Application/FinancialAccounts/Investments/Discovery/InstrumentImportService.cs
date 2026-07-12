using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public sealed class InstrumentImportService(
    IAlphaVantageClient alphaVantageClient,
    IAssetRepository assetRepository,
    IAssetListingRepository listingRepository,
    IMarketDataSymbolRepository symbolRepository,
    ILogger<InstrumentImportService> logger) : IInstrumentImportService
{
    public async Task<InstrumentImportPreviewDto> GetImportPreviewAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default)
    {
        var (asset, listing, symbol) = await FindExistingAsync(instrument, ct);
        return new InstrumentImportPreviewDto
        {
            Instrument = instrument,
            AssetAlreadyExists = asset is not null,
            ListingAlreadyExists = listing is not null,
            MarketDataSymbolAlreadyExists = symbol is not null,
            CanImport = HasRequiredImportData(instrument),
            Warnings = ImportWarnings(instrument)
        };
    }

    public async Task<ImportedInstrumentDto> ImportAsync(ImportInstrumentCommand command, CancellationToken ct = default)
    {
        var instrument = command.Instrument;
        ValidateImport(instrument);
        var warnings = ImportWarnings(instrument).ToList();
        var (asset, listing, symbol) = await FindExistingAsync(instrument, ct);
        var createdAsset = asset is null;
        asset ??= await assetRepository.Add(new Asset
        {
            Name = instrument.DisplayName,
            Type = ParseAssetType(instrument.SecurityType),
            Isin = instrument.Isin,
            ShareClassFigi = instrument.ShareClassFigi,
            CompositeFigi = instrument.CompositeFigi,
            BaseCurrency = instrument.TradingCurrency
        }, ct);

        var createdListing = listing is null;
        listing ??= await listingRepository.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = instrument.Ticker.Trim().ToUpperInvariant(),
            ExchangeMic = instrument.ExchangeMic!,
            ExchangeName = instrument.ExchangeName ?? instrument.ExchangeMic!,
            TradingCurrency = instrument.TradingCurrency!.ToUpperInvariant(),
            ListingFigi = instrument.ListingFigi,
            PriceMultiplier = InstrumentCurrencyUnits.IsMinor(instrument.TradingCurrency) ? 0.01m : 1m
        }, ct);

        var createdSymbol = symbol is null && !string.IsNullOrWhiteSpace(instrument.ProviderSymbol);
        if (createdSymbol)
        {
            var error = await ValidateSymbolAsync(instrument, ct);
            if (error is not null) warnings.Add(error);
            symbol = await symbolRepository.Add(new MarketDataSymbol
            {
                AssetListingId = listing.Id,
                Provider = MarketDataProvider.AlphaVantage,
                Symbol = instrument.ProviderSymbol!,
                ProviderExchangeCode = instrument.ExchangeCode,
                Currency = instrument.TradingCurrency,
                IsPrimary = true,
                IsEnabled = error is null,
                LastValidatedAt = error is null ? DateTimeOffset.UtcNow : null,
                LastError = error
            }, ct);
        }

        return new ImportedInstrumentDto
        {
            AssetId = asset.Id,
            AssetListingId = listing.Id,
            MarketDataSymbolId = symbol?.Id,
            Ticker = listing.Ticker,
            ExchangeName = listing.ExchangeName,
            TradingCurrency = listing.TradingCurrency,
            CreatedAsset = createdAsset,
            CreatedListing = createdListing,
            CreatedMarketDataSymbol = createdSymbol,
            Warnings = warnings
        };
    }

    private async Task<(Asset? Asset, AssetListing? Listing, MarketDataSymbol? Symbol)> FindExistingAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct)
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
                x.Type == ParseAssetType(instrument.SecurityType) && string.Equals(x.Name.Trim(), instrument.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase));

        AssetListing? listing = null;
        if (!string.IsNullOrWhiteSpace(instrument.ExchangeMic) && !string.IsNullOrWhiteSpace(instrument.TradingCurrency))
        {
            listing = await listingRepository.Get(instrument.Ticker, instrument.ExchangeMic, instrument.TradingCurrency, ct);
            if (listing is not null)
                asset = await assetRepository.Get(listing.AssetId, ct);
        }
        if (asset is not null)
        {
            var listings = await listingRepository.GetByAsset(asset.Id, ct);
            if (!string.IsNullOrWhiteSpace(instrument.ListingFigi))
                listing = listings.FirstOrDefault(x => x.ListingFigi == instrument.ListingFigi);
            listing ??= listings.FirstOrDefault(x =>
                x.Ticker.Equals(instrument.Ticker, StringComparison.OrdinalIgnoreCase)
                && x.ExchangeMic.Equals(instrument.ExchangeMic, StringComparison.OrdinalIgnoreCase)
                && x.TradingCurrency.Equals(instrument.TradingCurrency, StringComparison.OrdinalIgnoreCase));
        }

        MarketDataSymbol? symbol = null;
        if (listing is not null && !string.IsNullOrWhiteSpace(instrument.ProviderSymbol))
            symbol = (await symbolRepository.GetByListing(listing.Id, ct)).FirstOrDefault(x =>
                x.Provider == MarketDataProvider.AlphaVantage && x.Symbol.Equals(instrument.ProviderSymbol, StringComparison.OrdinalIgnoreCase));
        return (asset, listing, symbol);
    }

    private async Task<string?> ValidateSymbolAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct)
    {
        try
        {
            var prices = await alphaVantageClient.GetDailySeries(instrument.ProviderSymbol!, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow,
                new Currency { ShortName = instrument.TradingCurrency! }, ct);
            return prices.Any(x => x.PricePerUnit > 0) ? null : "Alpha Vantage returned no positive recent price; the symbol was saved disabled.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Alpha Vantage validation failed for {Symbol}", instrument.ProviderSymbol);
            return "Alpha Vantage validation failed; the symbol was saved disabled.";
        }
    }

    private static IReadOnlyList<string> ImportWarnings(InstrumentDiscoveryResultDto instrument) =>
        [.. instrument.Warnings,
            .. (HasRequiredImportData(instrument) ? [] : new[] { "Name, ticker, exchange MIC, and trading currency must be confirmed before import." }),
            .. (InstrumentCurrencyUnits.IsMinor(instrument.TradingCurrency)
                ? new[] { "Minor-unit currency detected; price multiplier will be set to 0.01." } : [])];

    private static void ValidateImport(InstrumentDiscoveryResultDto instrument)
    {
        if (!HasRequiredImportData(instrument))
            throw new ArgumentException("Name, ticker, exchange MIC, and trading currency must be confirmed before import.");
        if (ParseAssetType(instrument.SecurityType) is not (AssetType.Stock or AssetType.ETF))
            throw new ArgumentException("Only stocks and ETFs can be imported.");
    }

    private static bool HasRequiredImportData(InstrumentDiscoveryResultDto instrument) =>
        !string.IsNullOrWhiteSpace(instrument.DisplayName) && !string.IsNullOrWhiteSpace(instrument.Ticker)
        && !string.IsNullOrWhiteSpace(instrument.ExchangeMic) && !string.IsNullOrWhiteSpace(instrument.TradingCurrency);

    private static AssetType ParseAssetType(string? securityType) =>
        securityType?.Contains("ETF", StringComparison.OrdinalIgnoreCase) == true ? AssetType.ETF
        : securityType?.Contains("Equity", StringComparison.OrdinalIgnoreCase) == true || securityType?.Contains("Stock", StringComparison.OrdinalIgnoreCase) == true ? AssetType.Stock
        : AssetType.Other;
}

internal static class InstrumentCurrencyUnits
{
    private static readonly HashSet<string> _minor = new(StringComparer.OrdinalIgnoreCase) { "GBX", "GBP.", "ZAC", "ILA" };

    public static bool IsMinor(string? currency) => currency is not null && _minor.Contains(currency);
}