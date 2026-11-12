using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Discovery;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InstrumentCatalogMatcherTests
{
    private readonly Mock<IAssetRepository> _assetRepository = new();
    private readonly Mock<IAssetListingRepository> _listingRepository = new();
    private readonly Mock<IMarketDataSymbolRepository> _symbolRepository = new();

    [Fact]
    public async Task FindExistingAsync_WhenProviderSymbolHasListing_ReturnsCatalogRecordsImmediately()
    {
        var asset = new Asset { Id = 1, Name = "Existing ETF" };
        var listing = new AssetListing { Id = 2, AssetId = 1, Ticker = "CSPX" };
        var symbol = new MarketDataSymbol
        {
            Id = 3,
            AssetListingId = 2,
            Provider = MarketDataProvider.AlphaVantage,
            Symbol = "CSPX.LON"
        };
        _symbolRepository.Setup(x => x.Get(MarketDataProvider.AlphaVantage, "CSPX.LON", It.IsAny<CancellationToken>()))
            .ReturnsAsync(symbol);
        _listingRepository.Setup(x => x.Get(2, It.IsAny<CancellationToken>())).ReturnsAsync(listing);
        _assetRepository.Setup(x => x.Get(1, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var result = await CreateMatcher().FindExistingAsync(Instrument(), TestContext.Current.CancellationToken);

        Assert.Same(asset, result.Asset);
        Assert.Same(listing, result.Listing);
        Assert.Same(symbol, result.Symbol);
        _assetRepository.Verify(x => x.GetByShareClassFigi(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _assetRepository.Verify(x => x.GetAll(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindExistingAsync_WhenAssetMatchesShareClassFigi_MatchesListingAndSymbolCaseInsensitively()
    {
        var asset = new Asset { Id = 1, Name = "Existing ETF", ShareClassFigi = "BBGSC" };
        var listing = new AssetListing { Id = 2, AssetId = 1, Ticker = "CSPX", ListingFigi = "bbg001" };
        var symbol = new MarketDataSymbol
        {
            Id = 3,
            AssetListingId = 2,
            Provider = MarketDataProvider.AlphaVantage,
            Symbol = "cspx.lon"
        };
        _assetRepository.Setup(x => x.GetByShareClassFigi("BBGSC", It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _listingRepository.Setup(x => x.GetByAsset(1, It.IsAny<CancellationToken>())).ReturnsAsync([listing]);
        _symbolRepository.Setup(x => x.GetByListing(2, It.IsAny<CancellationToken>())).ReturnsAsync([symbol]);

        var result = await CreateMatcher().FindExistingAsync(Instrument(), TestContext.Current.CancellationToken);

        Assert.Same(asset, result.Asset);
        Assert.Same(listing, result.Listing);
        Assert.Same(symbol, result.Symbol);
        _assetRepository.Verify(x => x.GetByShareClassFigi("BBGSC", It.IsAny<CancellationToken>()), Times.Once);
    }

    private InstrumentCatalogMatcher CreateMatcher() =>
        new(_assetRepository.Object, _listingRepository.Object, _symbolRepository.Object);

    private static InstrumentDiscoveryResultDto Instrument() => new()
    {
        Source = "Combined",
        DisplayName = "Existing ETF",
        Ticker = "CSPX",
        ExchangeMic = "XLON",
        TradingCurrency = "USD",
        SecurityType = "ETF",
        ShareClassFigi = "BBGSC",
        ListingFigi = "BBG001",
        ProviderSymbol = "CSPX.LON"
    };
}