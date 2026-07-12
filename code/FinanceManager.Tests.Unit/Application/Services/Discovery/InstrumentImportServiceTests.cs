using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Discovery;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InstrumentImportServiceTests
{
    private readonly Mock<IAlphaVantageClient> _avClientMock = new();
    private readonly Mock<IAssetRepository> _assetRepositoryMock = new();
    private readonly Mock<IAssetListingRepository> _listingRepositoryMock = new();
    private readonly Mock<IMarketDataSymbolRepository> _symbolRepositoryMock = new();
    private readonly ILogger<InstrumentImportService> _logger =
        LoggerFactory.Create(_ => { }).CreateLogger<InstrumentImportService>();

    private InstrumentImportService CreateService() =>
        new(_avClientMock.Object, _assetRepositoryMock.Object, _listingRepositoryMock.Object,
            _symbolRepositoryMock.Object, _logger);

    private static InstrumentDiscoveryResultDto Instrument() => new()
    {
        DisplayName = "iShares Core S&P 500 UCITS ETF",
        Ticker = "CSPX",
        ExchangeMic = "XLON",
        ExchangeCode = "LN",
        TradingCurrency = "USD",
        SecurityType = "ETF",
        ShareClassFigi = "BBGSC",
        ListingFigi = "BBG001",
        ProviderSymbol = "CSPX.LON"
    };

    [Fact]
    public async Task PreviewAndImport_WhenInstrumentExists_ReusesAllRecords()
    {
        var asset = new Asset { Id = 1, Name = "iShares Core S&P 500 UCITS ETF", ShareClassFigi = "BBGSC" };
        var listing = new AssetListing { Id = 2, AssetId = 1, Ticker = "CSPX", ExchangeMic = "XLON", TradingCurrency = "USD", ListingFigi = "BBG001" };
        var symbol = new MarketDataSymbol { Id = 3, AssetListingId = 2, Provider = MarketDataProvider.AlphaVantage, Symbol = "CSPX.LON" };
        _assetRepositoryMock.Setup(x => x.GetByShareClassFigi("BBGSC", It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _listingRepositoryMock.Setup(x => x.GetByAsset(1, It.IsAny<CancellationToken>())).ReturnsAsync([listing]);
        _symbolRepositoryMock.Setup(x => x.GetByListing(2, It.IsAny<CancellationToken>())).ReturnsAsync([symbol]);

        var preview = await CreateService().GetImportPreviewAsync(Instrument(), TestContext.Current.CancellationToken);
        var result = await CreateService().ImportAsync(new ImportInstrumentCommand(Instrument()), TestContext.Current.CancellationToken);

        Assert.True(preview.AssetAlreadyExists);
        Assert.True(preview.ListingAlreadyExists);
        Assert.True(preview.MarketDataSymbolAlreadyExists);
        Assert.False(result.CreatedAsset);
        Assert.False(result.CreatedListing);
        Assert.False(result.CreatedMarketDataSymbol);
        _assetRepositoryMock.Verify(x => x.Add(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _listingRepositoryMock.Verify(x => x.Add(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()), Times.Never);
        _symbolRepositoryMock.Verify(x => x.Add(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenValidationReturnsNoPrices_SavesDisabledSymbol()
    {
        _assetRepositoryMock.Setup(x => x.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _assetRepositoryMock.Setup(x => x.Add(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset asset, CancellationToken _) => { asset.Id = 1; return asset; });
        _listingRepositoryMock.Setup(x => x.Add(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetListing listing, CancellationToken _) => { listing.Id = 2; return listing; });
        _symbolRepositoryMock.Setup(x => x.Add(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketDataSymbol symbol, CancellationToken _) => { symbol.Id = 3; return symbol; });
        _avClientMock.Setup(x => x.GetDailySeries("CSPX.LON", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<FinanceManager.Domain.FinancialAccounts.Currencies.Entities.Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().ImportAsync(new ImportInstrumentCommand(Instrument()), TestContext.Current.CancellationToken);

        Assert.True(result.CreatedMarketDataSymbol);
        Assert.Contains(result.Warnings, warning => warning.Contains("saved disabled", StringComparison.OrdinalIgnoreCase));
        _symbolRepositoryMock.Verify(x => x.Add(It.Is<MarketDataSymbol>(symbol => !symbol.IsEnabled && symbol.LastError != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenProviderSymbolExists_ReusesItsListingAndAsset()
    {
        var asset = new Asset { Id = 1, Name = "Existing ETF" };
        var listing = new AssetListing { Id = 2, AssetId = 1, Ticker = "CSPX", ExchangeMic = "XLON", TradingCurrency = "USD", ExchangeName = "London Stock Exchange" };
        var symbol = new MarketDataSymbol { Id = 3, AssetListingId = 2, Provider = MarketDataProvider.AlphaVantage, Symbol = "CSPX.LON" };
        _symbolRepositoryMock.Setup(x => x.Get(MarketDataProvider.AlphaVantage, "CSPX.LON", It.IsAny<CancellationToken>())).ReturnsAsync(symbol);
        _listingRepositoryMock.Setup(x => x.Get(2, It.IsAny<CancellationToken>())).ReturnsAsync(listing);
        _assetRepositoryMock.Setup(x => x.Get(1, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var result = await CreateService().ImportAsync(new ImportInstrumentCommand(Instrument()), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.AssetId);
        Assert.Equal(2, result.AssetListingId);
        Assert.Equal(3, result.MarketDataSymbolId);
        Assert.False(result.CreatedAsset);
        Assert.False(result.CreatedListing);
        Assert.False(result.CreatedMarketDataSymbol);
    }
}