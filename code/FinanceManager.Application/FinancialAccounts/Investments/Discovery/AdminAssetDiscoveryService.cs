using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public sealed class AdminAssetDiscoveryService(
    IInvestmentInstrumentDiscoveryService discovery,
    MarketDataSymbolResolutionService symbols,
    IAssetRepository assets,
    IAssetListingRepository listings,
    IMarketDataSymbolRepository symbolRepository,
    ICurrencyRepository currencies)
{
    public Task<IReadOnlyList<InstrumentDiscoveryResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (query.Length > 200) throw new ArgumentException("Search must be 200 characters or fewer.");
        return discovery.SearchAsync(query, ct);
    }

    public async Task<AdminAssetDraftDto> GetDraftAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instrument.DisplayName))
            throw new ArgumentException("Choose an external result with an instrument name.");

        // Name searches can initially return only a price-provider match. Resolve its exact
        // ticker/venue/currency through discovery before expanding a canonical share class.
        if (string.IsNullOrWhiteSpace(instrument.Isin) && string.IsNullOrWhiteSpace(instrument.ShareClassFigi)
            && !string.IsNullOrWhiteSpace(instrument.Ticker))
        {
            var identities = (await discovery.SearchAsync(instrument.Ticker, ct))
                .Where(x => SameListing(x, instrument) && (!string.IsNullOrWhiteSpace(x.Isin) || !string.IsNullOrWhiteSpace(x.ShareClassFigi)))
                .GroupBy(x => $"{x.Isin}:{x.ShareClassFigi}", StringComparer.OrdinalIgnoreCase).ToList();
            if (identities.Count > 1) throw new ArgumentException("Several share classes match this result. Search by ISIN to confirm the instrument.");
            if (identities.Count == 1) instrument = identities[0].First();
        }

        var found = (await discovery.DiscoverListingsAsync(instrument.Isin, instrument.ShareClassFigi, ct)).ToList();
        if (!found.Any(x => SameListing(x, instrument))) found.Insert(0, instrument);
        return new AdminAssetDraftDto
        {
            Asset = new AssetDto
            {
                Name = instrument.DisplayName,
                Type = InstrumentImportService.ParseAssetType(instrument.SecurityType),
                Isin = instrument.Isin,
                ShareClassFigi = instrument.ShareClassFigi,
                CompositeFigi = instrument.CompositeFigi
                // A listing's trading currency does not establish the asset's reporting currency.
            },
            Listings = found
        };
    }

    public async Task<IReadOnlyList<InstrumentDiscoveryResultDto>> FindListingsAsync(AssetDto asset, CancellationToken ct = default)
    {
        var existing = asset.Listings ?? [];
        if (asset.Id > 0)
        {
            var stored = await assets.Get(asset.Id, ct) ?? throw new KeyNotFoundException("Asset not found.");
            if (!SameIdentity(asset.Isin, stored.Isin) || !SameIdentity(asset.ShareClassFigi, stored.ShareClassFigi))
                throw new ArgumentException("Save the asset's identity changes before finding listings.");
            existing = stored.Listings.Select(x => x.ToDto()).ToList();
            asset = stored.ToDto();
        }
        if (string.IsNullOrWhiteSpace(asset.Isin) && string.IsNullOrWhiteSpace(asset.ShareClassFigi))
            throw new ArgumentException("Set an ISIN or share-class FIGI before finding listings.");
        var found = await discovery.DiscoverListingsAsync(asset.Isin, asset.ShareClassFigi, ct);
        return found.Where(candidate => !existing.Any(listing => SameListing(candidate, listing))).ToList();
    }

    public async Task<IReadOnlyList<MarketDataSymbolResolution>> ResolveSymbolsAsync(InstrumentDiscoveryResultDto instrument, long? listingId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instrument.Ticker) || string.IsNullOrWhiteSpace(instrument.ExchangeMic)
            || string.IsNullOrWhiteSpace(instrument.TradingCurrency))
            throw new ArgumentException("Confirm the ticker, exchange MIC and trading currency before finding symbols.");
        if (listingId is > 0)
        {
            var listing = await listings.Get(listingId.Value, ct) ?? throw new KeyNotFoundException("Listing not found.");
            if (!string.Equals(instrument.Ticker, listing.Ticker, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(instrument.ExchangeMic, listing.ExchangeMic, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(instrument.TradingCurrency, listing.TradingCurrency, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(instrument.ListingFigi, listing.ListingFigi, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save the listing's ticker, venue and currency changes before finding symbols.");
            var asset = await assets.Get(listing.AssetId, ct) ?? throw new KeyNotFoundException("Asset not found.");
            instrument = new InstrumentDiscoveryResultDto
            {
                DisplayName = asset.Name,
                SecurityType = asset.Type.ToString(),
                Isin = asset.Isin,
                ShareClassFigi = asset.ShareClassFigi,
                CompositeFigi = asset.CompositeFigi,
                Ticker = listing.Ticker,
                ExchangeMic = listing.ExchangeMic,
                ExchangeName = listing.ExchangeName,
                TradingCurrency = listing.TradingCurrency,
                ListingFigi = listing.ListingFigi,
                ProviderSymbol = listing.MarketDataSymbols.FirstOrDefault(x => x.Provider == MarketDataProvider.AlphaVantage)?.Symbol
            };
        }
        return await symbols.ResolveAsync(instrument, ct);
    }

    public async Task<AssetDto> CreateAsync(AssetDto draft, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(draft.Name)) throw new ArgumentException("Asset name is required.");
        if (!Enum.IsDefined(draft.Type)) throw new ArgumentException("Choose a valid asset type.");
        await ValidateCurrency(draft.BaseCurrency, false, ct);
        if (!string.IsNullOrWhiteSpace(draft.Isin) && await assets.GetByIsin(draft.Isin.Trim(), ct) is not null
            || !string.IsNullOrWhiteSpace(draft.ShareClassFigi) && await assets.GetByShareClassFigi(draft.ShareClassFigi.Trim(), ct) is not null)
            throw new ArgumentException("This asset already exists. Open its editor to add listings or symbols.");
        await ValidateListings(draft.Listings, ct);

        var asset = draft.ToNewEntity();
        asset.Listings = draft.Listings.Select(x => ToListingGraph(x, 0)).ToList();
        // One repository insert saves the complete reviewed graph in a single SaveChanges transaction.
        return (await assets.Add(asset, ct)).ToDto();
    }

    public async Task<AssetListingDto> AddListingAsync(long assetId, AssetListingDto draft, CancellationToken ct = default)
    {
        var asset = await assets.Get(assetId, ct) ?? throw new KeyNotFoundException("Asset not found.");
        if (draft.IsPrimaryListing && asset.Listings.Any(x => x.IsPrimaryListing))
            throw new ArgumentException("The asset already has a primary listing. Add this listing without changing its primary setting.");
        await ValidateListings([draft], ct);
        return (await listings.Add(ToListingGraph(draft, assetId), ct)).ToDto();
    }

    private async Task ValidateListings(List<AssetListingDto> drafts, CancellationToken ct)
    {
        if (drafts is null) throw new ArgumentException("Listings must be supplied as a list.");
        if (drafts.Any(x => x is null)) throw new ArgumentException("A selected listing cannot be empty.");
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var symbolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (drafts.Count(x => x.IsPrimaryListing) > 1) throw new ArgumentException("Choose only one primary listing.");
        foreach (var draft in drafts)
        {
            if (string.IsNullOrWhiteSpace(draft.Ticker) || string.IsNullOrWhiteSpace(draft.ExchangeMic)
                || string.IsNullOrWhiteSpace(draft.ExchangeName) || string.IsNullOrWhiteSpace(draft.TradingCurrency))
                throw new ArgumentException("Each selected listing needs a ticker, exchange MIC, exchange name and trading currency.");
            if (draft.PriceMultiplier <= 0m) throw new ArgumentException("Price multiplier must be greater than zero.");
            await ValidateCurrency(draft.TradingCurrency, true, ct);
            var key = $"{draft.Ticker.Trim()}:{draft.ExchangeMic.Trim()}:{draft.TradingCurrency.Trim()}";
            if (!keys.Add(key) || await listings.Get(draft.Ticker.Trim(), draft.ExchangeMic.Trim(), draft.TradingCurrency.Trim(), ct) is not null)
                throw new ArgumentException($"Listing {draft.Ticker} / {draft.ExchangeMic} / {draft.TradingCurrency} already exists.");
            if (draft.MarketDataSymbols is null) throw new ArgumentException("Provider symbols must be supplied as a list.");
            if (draft.MarketDataSymbols.Any(x => x is null)) throw new ArgumentException("A provider symbol cannot be empty.");
            if (draft.MarketDataSymbols.Count(x => x.IsPrimary && x.IsEnabled) > 1)
                throw new ArgumentException("Choose only one enabled primary symbol per listing.");
            foreach (var symbol in draft.MarketDataSymbols)
            {
                if (!Enum.IsDefined(symbol.Provider) || string.IsNullOrWhiteSpace(symbol.Symbol))
                    throw new ArgumentException("Each provider symbol needs a valid provider and non-empty symbol.");
                if (!symbolKeys.Add($"{symbol.Provider}:{symbol.Symbol.Trim()}")
                    || await symbolRepository.Get(symbol.Provider, symbol.Symbol.Trim(), ct) is not null)
                    throw new ArgumentException($"{symbol.Provider} symbol {symbol.Symbol} already exists; its mapping was preserved.");
            }
        }
    }

    private async Task ValidateCurrency(string? code, bool trading, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        if (trading && DefaultCurrency.MinorQuoteUnits.ContainsKey(code.Trim())) return;
        if (await currencies.GetByCode(code.Trim(), ct) is null)
            throw new ArgumentException("Choose a predefined currency.");
    }

    private static AssetListing ToListingGraph(AssetListingDto draft, long assetId)
    {
        var listing = draft.ToNewEntity(assetId);
        listing.MarketDataSymbols = draft.MarketDataSymbols.Select(x => x.ToNewEntity(0)).ToList();
        var now = DateTimeOffset.UtcNow;
        listing.CreatedAt = listing.UpdatedAt = now;
        foreach (var symbol in listing.MarketDataSymbols)
            symbol.CreatedAt = symbol.UpdatedAt = now;
        return listing;
    }

    private static bool SameListing(InstrumentDiscoveryResultDto a, InstrumentDiscoveryResultDto b) =>
        !string.IsNullOrWhiteSpace(a.ListingFigi) && string.Equals(a.ListingFigi, b.ListingFigi, StringComparison.OrdinalIgnoreCase)
        || string.Equals(a.Ticker, b.Ticker, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.ExchangeMic, b.ExchangeMic, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.TradingCurrency, b.TradingCurrency, StringComparison.OrdinalIgnoreCase);

    private static bool SameListing(InstrumentDiscoveryResultDto candidate, AssetListingDto listing) =>
        !string.IsNullOrWhiteSpace(candidate.ListingFigi) && string.Equals(candidate.ListingFigi, listing.ListingFigi, StringComparison.OrdinalIgnoreCase)
        || string.Equals(candidate.Ticker, listing.Ticker, StringComparison.OrdinalIgnoreCase)
        && string.Equals(candidate.ExchangeMic, listing.ExchangeMic, StringComparison.OrdinalIgnoreCase)
        && string.Equals(candidate.TradingCurrency, listing.TradingCurrency, StringComparison.OrdinalIgnoreCase);

    private static bool SameIdentity(string? a, string? b) =>
        string.Equals(a?.Trim() ?? string.Empty, b?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}