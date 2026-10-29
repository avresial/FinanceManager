using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Discovery;

[Trait("Category", "Unit")]
public class AdminAssetDiscoveryServiceTests
{
    private readonly Mock<IInvestmentInstrumentDiscoveryService> _discovery = new();
    private readonly Mock<IAssetRepository> _assets = new();
    private readonly Mock<IAssetListingRepository> _listings = new();
    private readonly Mock<IMarketDataSymbolRepository> _symbols = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<IMarketDataSymbolResolver> _resolver = new();

    public AdminAssetDiscoveryServiceTests()
    {
        _currencies.Setup(x => x.GetByCode(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => code is "EUR" or "USD" ? new Currency(1, code, code) : null);
        _resolver.SetupGet(x => x.Provider).Returns(MarketDataProvider.AlphaVantage);
        _resolver.SetupGet(x => x.Priority).Returns(1);
        _resolver.Setup(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketDataSymbolResolution(MarketDataProvider.AlphaVantage, 1, "resolved", "VWCE.DEX"));
    }

    private AdminAssetDiscoveryService Service() => new(_discovery.Object, new([_resolver.Object]), _assets.Object,
        _listings.Object, _symbols.Object, _currencies.Object);

    private static InstrumentDiscoveryResultDto Instrument(string ticker = "VWCE", string mic = "XETR", string currency = "EUR", string figi = "BBGL1") => new()
    {
        DisplayName = "Vanguard FTSE All-World",
        SecurityType = "ETF",
        Isin = "IE00BK5BQT80",
        ShareClassFigi = "BBGSC",
        Ticker = ticker,
        ExchangeMic = mic,
        ExchangeName = mic,
        TradingCurrency = currency,
        ListingFigi = figi
    };

    private static AssetListingDto Listing(string ticker = "VWCE", string mic = "XETR", string currency = "EUR") => new()
    {
        Ticker = ticker,
        ExchangeMic = mic,
        ExchangeName = mic,
        TradingCurrency = currency,
        MarketDataSymbols = [new() { Provider = MarketDataProvider.AlphaVantage, Symbol = $"{ticker}.TEST", IsPrimary = true }]
    };

    private static AssetDto Draft() => new()
    {
        Name = "Reviewed name",
        Type = AssetType.ETF,
        Isin = "IE00BK5BQT80",
        ShareClassFigi = "BBGSC",
        Issuer = "Reviewed issuer",
        Listings = [Listing(), Listing("VWRA", "XLON", "USD")]
    };

    [Fact]
    public async Task Draft_ExpandsShareClassAndDoesNotGuessBaseCurrencyOrPersist()
    {
        _discovery.Setup(x => x.DiscoverListingsAsync("IE00BK5BQT80", "BBGSC", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Instrument(), Instrument("VWRA", "XLON", "USD", "BBGL2")]);

        var draft = await Service().GetDraftAsync(Instrument(), TestContext.Current.CancellationToken);

        Assert.Equal(2, draft.Listings.Count);
        Assert.Equal(AssetType.ETF, draft.Asset.Type);
        Assert.Equal("BBGSC", draft.Asset.ShareClassFigi);
        Assert.Null(draft.Asset.BaseCurrency);
        Assert.Null(draft.Asset.Issuer);
        _assets.Verify(x => x.Add(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _resolver.Verify(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindListings_ExcludesStoredListingsAndPreservesCuratedFields()
    {
        var stored = new Asset
        {
            Id = 10,
            Name = "Curated name",
            Issuer = "Curated issuer",
            ShareClassFigi = "BBGSC",
            Listings = [new()
        { Ticker = "VWCE", ExchangeMic = "XETR", TradingCurrency = "EUR", ListingFigi = "BBGL1" }]
        };
        _assets.Setup(x => x.Get(10, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _discovery.Setup(x => x.DiscoverListingsAsync(null, "BBGSC", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Instrument(), Instrument("VWRA", "XLON", "USD", "BBGL2")]);

        var result = await Service().FindListingsAsync(new() { Id = 10, ShareClassFigi = "BBGSC" }, TestContext.Current.CancellationToken);

        Assert.Equal("VWRA", Assert.Single(result).Ticker);
        Assert.Equal("Curated name", stored.Name);
        Assert.Equal("Curated issuer", stored.Issuer);
        _assets.Verify(x => x.Update(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_UsesOneGraphInsertWithReviewedMetadataAndSymbols()
    {
        Asset? inserted = null;
        _assets.Setup(x => x.Add(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).ReturnsAsync((Asset asset, CancellationToken _) =>
        { inserted = asset; asset.Id = 10; return asset; });

        var result = await Service().CreateAsync(Draft(), TestContext.Current.CancellationToken);

        Assert.Equal(10, result.Id);
        Assert.Equal("Reviewed issuer", result.Issuer);
        Assert.NotNull(inserted);
        Assert.Equal(2, inserted.Listings.Count);
        Assert.All(inserted.Listings, listing =>
        {
            Assert.Single(listing.MarketDataSymbols);
            Assert.NotEqual(default, listing.CreatedAt);
            Assert.NotEqual(default, listing.MarketDataSymbols.Single().CreatedAt);
        });
        _assets.Verify(x => x.Add(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Once);
        _listings.Verify(x => x.Add(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()), Times.Never);
        _resolver.Verify(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("asset")]
    [InlineData("listing")]
    [InlineData("symbol")]
    public async Task Create_DuplicatesAreRejectedWithoutChangingExistingData(string duplicate)
    {
        if (duplicate == "asset") _assets.Setup(x => x.GetByIsin("IE00BK5BQT80", It.IsAny<CancellationToken>())).ReturnsAsync(new Asset());
        if (duplicate == "listing") _listings.Setup(x => x.Get("VWCE", "XETR", "EUR", It.IsAny<CancellationToken>())).ReturnsAsync(new AssetListing());
        if (duplicate == "symbol") _symbols.Setup(x => x.Get(MarketDataProvider.AlphaVantage, "VWCE.TEST", It.IsAny<CancellationToken>())).ReturnsAsync(new MarketDataSymbol { IsEnabled = false });

        await Assert.ThrowsAsync<ArgumentException>(() => Service().CreateAsync(Draft(), TestContext.Current.CancellationToken));

        _assets.Verify(x => x.Add(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _assets.Verify(x => x.Upsert(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _symbols.Verify(x => x.Update(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_InvalidLaterListingRejectsEntireGraphBeforeInsert()
    {
        var draft = Draft();
        draft.Listings[1].TradingCurrency = "UNKNOWN";

        await Assert.ThrowsAsync<ArgumentException>(() => Service().CreateAsync(draft, TestContext.Current.CancellationToken));

        _assets.Verify(x => x.Add(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddListing_PreservesCuratedAssetAndUsesNoUpserts()
    {
        var asset = new Asset { Id = 10, Name = "Curated", Issuer = "Manual" };
        _assets.Setup(x => x.Get(10, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _listings.Setup(x => x.Add(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetListing listing, CancellationToken _) => listing);

        var result = await Service().AddListingAsync(10, Listing(), TestContext.Current.CancellationToken);

        Assert.Equal(10, result.AssetId);
        Assert.Equal("Curated", asset.Name);
        Assert.Equal("Manual", asset.Issuer);
        _assets.Verify(x => x.Update(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _symbols.Verify(x => x.Upsert(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveStoredListing_RejectsUnsavedVenueChangesBeforeProviderCalls()
    {
        _listings.Setup(x => x.Get(20, It.IsAny<CancellationToken>())).ReturnsAsync(new AssetListing
        { Id = 20, Ticker = "VWCE", ExchangeMic = "XETR", TradingCurrency = "EUR", ListingFigi = "BBGL1" });

        await Assert.ThrowsAsync<ArgumentException>(() => Service().ResolveSymbolsAsync(Instrument(mic: "XLON"), 20, TestContext.Current.CancellationToken));

        _resolver.Verify(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task Draft_NameSearchResultResolvesCanonicalIdentityBeforeExpandingVenues()
    {
        var priceProviderResult = new InstrumentDiscoveryResultDto
        { DisplayName = "Vanguard ETF", Ticker = "VWCE", ExchangeMic = "XETR", TradingCurrency = "EUR", SecurityType = "ETF" };
        _discovery.Setup(x => x.SearchAsync("VWCE", It.IsAny<CancellationToken>())).ReturnsAsync([Instrument()]);
        _discovery.Setup(x => x.DiscoverListingsAsync("IE00BK5BQT80", "BBGSC", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Instrument(), Instrument("VWRA", "XLON", "USD", "BBGL2")]);

        var draft = await Service().GetDraftAsync(priceProviderResult, TestContext.Current.CancellationToken);

        Assert.Equal("BBGSC", draft.Asset.ShareClassFigi);
        Assert.Equal(2, draft.Listings.Count);
    }

    [Fact]
    public async Task FindListings_RejectsUnsavedAssetIdentityBeforeCallingProviders()
    {
        _assets.Setup(x => x.Get(10, It.IsAny<CancellationToken>())).ReturnsAsync(new Asset { Id = 10, ShareClassFigi = "STORED" });

        await Assert.ThrowsAsync<ArgumentException>(() => Service().FindListingsAsync(new() { Id = 10, ShareClassFigi = "CHANGED" }, TestContext.Current.CancellationToken));

        _discovery.Verify(x => x.DiscoverListingsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}