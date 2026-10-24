using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
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

    private static InstrumentDiscoveryResultDto Instrument(
        string securityType = "ETF",
        string? tradingCurrency = "USD",
        string? providerSymbol = "CSPX.LON",
        string? displayName = "iShares Core S&P 500 UCITS ETF",
        string? ticker = "CSPX",
        string? exchangeMic = "XLON",
        IReadOnlyList<string>? warnings = null) => new()
        {
            Source = "Combined",
            DisplayName = displayName!,
            Ticker = ticker!,
            ExchangeMic = exchangeMic,
            ExchangeCode = "LN",
            TradingCurrency = tradingCurrency,
            SecurityType = securityType,
            ShareClassFigi = "BBGSC",
            ListingFigi = "BBG001",
            ProviderSymbol = providerSymbol,
            Warnings = warnings ?? []
        };

    private static StockPrice PositivePrice() => new()
    {
        Isin = "CSPX.LON",
        PricePerUnit = 100m,
        Currency = new Currency(0, "USD", "USD"),
        Date = DateTime.UtcNow
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
        _assetRepositoryMock.Setup(x => x.Upsert(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset asset, CancellationToken _) => { asset.Id = 1; return asset; });
        _listingRepositoryMock.Setup(x => x.Upsert(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetListing listing, CancellationToken _) => { listing.Id = 2; return listing; });
        _symbolRepositoryMock.Setup(x => x.Upsert(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketDataSymbol symbol, CancellationToken _) => { symbol.Id = 3; return symbol; });
        _avClientMock.Setup(x => x.GetDailySeries("CSPX.LON", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().ImportAsync(new ImportInstrumentCommand(Instrument()), TestContext.Current.CancellationToken);

        Assert.True(result.CreatedMarketDataSymbol);
        Assert.Contains(result.Warnings, warning => warning.Contains("saved disabled", StringComparison.OrdinalIgnoreCase));
        _symbolRepositoryMock.Verify(x => x.Upsert(It.Is<MarketDataSymbol>(symbol => !symbol.IsEnabled && symbol.LastError != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportValidatedAsync_WhenSecurityTypeIsUnknown_RejectsBeforePersistence()
    {
        var instrument = Instrument("Bond");

        var exception = await Assert.ThrowsAsync<InstrumentImportException>(() =>
            CreateService().ImportValidatedAsync(instrument, TestContext.Current.CancellationToken));

        Assert.Equal("instrument_import_failed", exception.Code);
        _assetRepositoryMock.Verify(x => x.Upsert(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _avClientMock.Verify(x => x.GetDailySeries(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Theory]
    [InlineData("ETF", true)]
    [InlineData("ETP", true)]
    [InlineData("Equity", true)]
    [InlineData("Stock", true)]
    [InlineData("Bond", false)]
    [InlineData(null, false)]
    public void CanImportForTransaction_ClassifiesSecurityType(string? securityType, bool expected)
    {
        var instrument = Instrument(securityType ?? string.Empty);

        Assert.Equal(expected, CreateService().CanImportForTransaction(instrument));
    }

    [Fact]
    public void CanImportForTransaction_WithInformationalWarnings_RemainsImportable()
    {
        var instrument = Instrument(warnings:
        [
            "The exchange mapping is ambiguous.",
            "Identity was not confirmed by OpenFIGI; check the exchange and currency before saving.",
            "Alpha Vantage quote currency differs from the listing trading currency."
        ]);

        Assert.True(CreateService().CanImportForTransaction(instrument));
    }

    [Theory]
    [InlineData(null, "CSPX", "XLON", "USD", "CSPX.LON")]
    [InlineData("Name", null, "XLON", "USD", "CSPX.LON")]
    [InlineData("Name", "CSPX", null, "USD", "CSPX.LON")]
    [InlineData("Name", "CSPX", "XLON", null, "CSPX.LON")]
    [InlineData("Name", "CSPX", "XLON", "USD", null)]
    public void CanImportForTransaction_WhenStructuralDataIsMissing_ReturnsFalse(
        string? displayName, string? ticker, string? exchangeMic, string? tradingCurrency, string? providerSymbol)
    {
        var instrument = Instrument(
            displayName: displayName,
            ticker: ticker,
            exchangeMic: exchangeMic,
            tradingCurrency: tradingCurrency,
            providerSymbol: providerSymbol);

        Assert.False(CreateService().CanImportForTransaction(instrument));
    }

    [Theory]
    [InlineData("The exchange mapping is ambiguous.")]
    [InlineData("Identity was not confirmed by OpenFIGI; check the exchange and currency before saving.")]
    [InlineData("Alpha Vantage quote currency differs from the listing trading currency.")]
    public async Task ValidateForTransactionAsync_WithLegacyWarningText_DoesNotBlockValidInstrument(string warning)
    {
        _avClientMock.Setup(x => x.GetDailySeries("CSPX.LON", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PositivePrice()]);

        var instrument = Instrument(warnings: [warning]);

        await CreateService().ValidateForTransactionAsync(instrument, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ValidateForTransactionAsync_WithAlphaVantageOnlyResult_AcceptsWhenDailySeriesHasPositivePrice()
    {
        _avClientMock.Setup(x => x.GetDailySeries("VWCE.DEX", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PositivePrice()]);

        var instrument = new InstrumentDiscoveryResultDto
        {
            Source = "AlphaVantage",
            DisplayName = "Vanguard FTSE All-World UCITS ETF",
            Ticker = "VWCE",
            ExchangeMic = "XETR",
            ExchangeCode = "XETRA",
            ExchangeName = "Xetra",
            TradingCurrency = "EUR",
            SecurityType = "ETF",
            ProviderSymbol = "VWCE.DEX",
            Warnings = ["Identity was not confirmed by OpenFIGI; check the exchange and currency before saving."]
        };

        await CreateService().ValidateForTransactionAsync(instrument, TestContext.Current.CancellationToken);

        _avClientMock.Verify(x => x.GetDailySeries("VWCE.DEX", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Currency>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportValidatedAsync_DoesNotRevalidateProviderSymbolAndPersistsEnabledSymbol()
    {
        Asset? persistedAsset = null;
        MarketDataSymbol? persistedSymbol = null;
        _assetRepositoryMock.Setup(x => x.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _assetRepositoryMock.Setup(x => x.Upsert(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset asset, CancellationToken _) => { asset.Id = 1; persistedAsset = asset; return asset; });
        _listingRepositoryMock.Setup(x => x.Upsert(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetListing listing, CancellationToken _) => { listing.Id = 2; return listing; });
        _symbolRepositoryMock.Setup(x => x.Upsert(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketDataSymbol symbol, CancellationToken _) => { symbol.Id = 3; persistedSymbol = symbol; return symbol; });
        _avClientMock.Setup(x => x.GetDailySeries(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ImportValidatedAsync must not call Alpha Vantage."));

        var result = await CreateService().ImportValidatedAsync(Instrument("ETP"), TestContext.Current.CancellationToken);

        Assert.True(result.CreatedMarketDataSymbol);
        Assert.Equal(AssetType.ETF, persistedAsset?.Type);
        Assert.NotNull(persistedSymbol);
        Assert.True(persistedSymbol.IsEnabled);
        Assert.NotNull(persistedSymbol.LastValidatedAt);
        Assert.Null(persistedSymbol.LastError);
        _avClientMock.Verify(x => x.GetDailySeries(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}