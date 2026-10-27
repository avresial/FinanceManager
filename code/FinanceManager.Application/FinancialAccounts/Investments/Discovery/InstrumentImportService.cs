using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public sealed class InstrumentImportService(
    MarketDataSymbolResolutionService symbolResolutionService,
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
            CanImport = HasRequiredImportData(instrument)
                && ParseAssetType(instrument.SecurityType) is AssetType.Stock or AssetType.ETF,
            Warnings = ImportWarnings(instrument)
        };
    }

    public async Task<ImportedInstrumentDto> ImportAsync(ImportInstrumentCommand command, CancellationToken ct = default)
    {
        ValidateImport(command.Instrument);
        var results = await symbolResolutionService.ResolveAsync(command.Instrument, ct);
        return await PersistAsync(command.Instrument, results, ct);
    }

    public async Task<ValidatedInstrumentImport> ValidateForTransactionAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default)
    {
        ThrowIfTransactionImportBlocked(instrument);
        var results = await symbolResolutionService.ResolveAsync(instrument, ct);
        if (!results.Any(x => x.Status == "resolved"))
            throw new InstrumentImportException(
                results.Any(x => x.Status == "provider unavailable") ? "provider_unavailable" : "market_data_symbol_invalid",
                "No configured provider could validate the selected instrument. No asset or transaction was created.");
        return new(instrument, results);
    }

    public async Task<ImportedInstrumentDto> ImportValidatedAsync(ValidatedInstrumentImport validated, CancellationToken ct = default)
    {
        // All network work completed before the caller entered its database transaction.
        ThrowIfTransactionImportBlocked(validated.Instrument);
        return await PersistAsync(validated.Instrument, validated.ProviderResults, ct);
    }

    public bool CanImportForTransaction(InstrumentDiscoveryResultDto instrument) =>
        GetTransactionImportBlocker(instrument) is null;

    private async Task<ImportedInstrumentDto> PersistAsync(
        InstrumentDiscoveryResultDto instrument,
        IReadOnlyList<MarketDataSymbolResolution> providerResults,
        CancellationToken ct)
    {
        var warnings = ImportWarnings(instrument).ToList();
        var (asset, listing, symbol) = await FindExistingAsync(instrument, ct);
        var createdAsset = asset is null;
        asset ??= await assetRepository.Upsert(new Asset
        {
            Name = instrument.DisplayName,
            Type = ParseAssetType(instrument.SecurityType),
            Isin = instrument.Isin,
            ShareClassFigi = instrument.ShareClassFigi,
            CompositeFigi = instrument.CompositeFigi,
            BaseCurrency = instrument.TradingCurrency!.ToUpperInvariant()
        }, ct);

        var metadata = providerResults.FirstOrDefault(x => x.Status == "resolved");
        var enriched = false;
        if (string.IsNullOrWhiteSpace(asset.Name) && !string.IsNullOrWhiteSpace(metadata?.Name))
        { asset.Name = metadata.Name; enriched = true; }
        if (asset.Type == AssetType.Other && ParseAssetType(metadata?.SecurityType) is var resolvedType && resolvedType != AssetType.Other)
        { asset.Type = resolvedType; enriched = true; }
        if (enriched) await assetRepository.Update(asset, ct);

        var createdListing = listing is null;
        listing ??= await listingRepository.Upsert(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = instrument.Ticker.Trim().ToUpperInvariant(),
            ExchangeMic = instrument.ExchangeMic!,
            ExchangeName = instrument.ExchangeName ?? metadata?.ExchangeName ?? instrument.ExchangeMic!,
            TradingCurrency = instrument.TradingCurrency!.ToUpperInvariant(),
            ListingFigi = instrument.ListingFigi,
            PriceMultiplier = InstrumentCurrencyUnits.IsMinor(instrument.TradingCurrency) ? 0.01m : 1m
        }, ct);

        if (string.IsNullOrWhiteSpace(listing.ExchangeName) && !string.IsNullOrWhiteSpace(metadata?.ExchangeName))
        {
            listing.ExchangeName = metadata.ExchangeName;
            await listingRepository.Update(listing, ct);
        }

        var createdSymbol = false;
        var existingSymbols = await symbolRepository.GetByListing(listing.Id, ct);
        var resolved = providerResults.Where(x => x.Status == "resolved").OrderBy(x => x.Priority).ToArray();
        var primaryAssigned = existingSymbols.Any(x => x.IsPrimary && x.IsEnabled);
        foreach (var result in providerResults.OrderBy(x => x.Priority))
        {
            if (result.Error is not null) warnings.Add($"{result.Provider}: {result.Error}");
            if (string.IsNullOrWhiteSpace(result.Symbol)) continue;
            var existing = existingSymbols.FirstOrDefault(x => x.Provider == result.Provider && string.Equals(x.Symbol, result.Symbol, StringComparison.OrdinalIgnoreCase))
                ?? await symbolRepository.Get(result.Provider, result.Symbol, ct);
            if (existing is not null && existing.AssetListingId != listing.Id)
            {
                warnings.Add($"{result.Provider}: symbol already belongs to another listing; mapping was not changed.");
                continue;
            }
            if (existing is not null)
            {
                // Preserve curated symbols/currency and enabled settings while refreshing diagnostics.
                if (result.Status == "resolved")
                {
                    existing.LastValidatedAt = DateTimeOffset.UtcNow;
                    if (!primaryAssigned && existing.IsEnabled) existing.IsPrimary = true;
                }
                existing.LastError = result.Error;
                primaryAssigned |= existing.IsPrimary && existing.IsEnabled;
                await symbolRepository.Update(existing, ct);
                symbol ??= existing;
                continue;
            }
            var saved = await symbolRepository.Upsert(new MarketDataSymbol
            {
                AssetListingId = listing.Id,
                Provider = result.Provider,
                Symbol = result.Symbol,
                ProviderExchangeCode = result.ExchangeCode,
                Currency = result.Currency,
                IsPrimary = result.Status == "resolved" && !primaryAssigned,
                IsEnabled = result.Status == "resolved",
                LastValidatedAt = result.Status == "resolved" ? DateTimeOffset.UtcNow : null,
                LastError = result.Error
            }, ct);
            if (saved.IsPrimary && saved.IsEnabled) primaryAssigned = true;
            symbol ??= saved;
            createdSymbol = true;
        }

        logger.LogInformation("Imported listing {ListingId} with {ResolvedCount} validated provider mappings", listing.Id, resolved.Length);
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
            ProviderResults = providerResults,
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

    // Structural blockers only. Informational discovery warnings (e.g. "identity was not confirmed
    // by OpenFIGI") are shown to the user but must not block saving a result that has everything the
    // import needs. Both validation and the search-time importability check read this one rule set so
    // they cannot drift.
    private static void ThrowIfTransactionImportBlocked(InstrumentDiscoveryResultDto instrument)
    {
        var blocker = GetTransactionImportBlocker(instrument);
        if (blocker is not null)
            throw blocker;
    }

    private static InstrumentImportException? GetTransactionImportBlocker(InstrumentDiscoveryResultDto instrument)
    {
        if (!HasRequiredImportData(instrument))
            return new InstrumentImportException(
                "instrument_not_found",
                "The selected instrument is missing a name, ticker, exchange, or trading currency.");
        if (ParseAssetType(instrument.SecurityType) is AssetType.Other)
            return new InstrumentImportException(
                "instrument_import_failed",
                "The selected instrument's asset type could not be determined; only stocks and ETFs can be added to an investment transaction.");
        return null;
    }

    private static bool HasRequiredImportData(InstrumentDiscoveryResultDto instrument) =>
        !string.IsNullOrWhiteSpace(instrument.DisplayName) && !string.IsNullOrWhiteSpace(instrument.Ticker)
        && !string.IsNullOrWhiteSpace(instrument.ExchangeMic) && !string.IsNullOrWhiteSpace(instrument.TradingCurrency);

    internal static AssetType ParseAssetType(string? securityType) =>
        securityType?.Contains("ETF", StringComparison.OrdinalIgnoreCase) == true
        || securityType?.Contains("ETP", StringComparison.OrdinalIgnoreCase) == true ? AssetType.ETF
        : securityType?.Contains("Equity", StringComparison.OrdinalIgnoreCase) == true || securityType?.Contains("Stock", StringComparison.OrdinalIgnoreCase) == true ? AssetType.Stock
        : AssetType.Other;
}

internal static class InstrumentCurrencyUnits
{
    private static readonly HashSet<string> _minor = new(StringComparer.OrdinalIgnoreCase) { "GBX", "GBP.", "ZAC", "ILA" };

    public static bool IsMinor(string? currency) => currency is not null && _minor.Contains(currency);
}