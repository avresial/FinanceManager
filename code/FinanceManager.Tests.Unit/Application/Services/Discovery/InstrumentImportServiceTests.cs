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

    public InstrumentImportServiceTests() =>
        _symbolRepositoryMock.Setup(x => x.GetByListing(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

    private InstrumentImportService CreateService() =>
        new(new MarketDataSymbolResolutionService([new TestResolver(_avClientMock.Object)]), _assetRepositoryMock.Object, _listingRepositoryMock.Object,
            _symbolRepositoryMock.Object, _logger);

    private static InstrumentDiscoveryResultDto Instrument(
        string? securityType = "ETF",
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
        Assert.Contains(result.Warnings, warning => warning.Contains("No positive recent price", StringComparison.OrdinalIgnoreCase));
        _symbolRepositoryMock.Verify(x => x.Upsert(It.Is<MarketDataSymbol>(symbol => !symbol.IsEnabled && symbol.LastError != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportValidatedAsync_WhenSecurityTypeIsExplicitlyUnsupported_RejectsBeforePersistence()
    {
        var instrument = Instrument("Bond");

        var exception = await Assert.ThrowsAsync<InstrumentImportException>(() =>
            CreateService().ImportValidatedAsync(new(instrument, []), TestContext.Current.CancellationToken));

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
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Bond", false)]
    [InlineData("Corporate Debt", false)]
    [InlineData("Option", false)]
    public void CanImportForTransaction_ClassifiesSecurityType(string? securityType, bool expected)
    {
        var instrument = Instrument(securityType);

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
    [InlineData(null, "CSPX")]
    [InlineData("   ", "CSPX")]
    [InlineData("Name", null)]
    [InlineData("Name", "   ")]
    public void CanImportForTransaction_WhenCoreIdentityIsMissing_ReturnsFalse(
        string? displayName, string? ticker)
    {
        var instrument = Instrument(displayName: displayName, ticker: ticker);

        Assert.False(CreateService().CanImportForTransaction(instrument));
    }

    [Theory]
    [InlineData(null, "USD", "CSPX.LON")]
    [InlineData("XLON", null, "CSPX.LON")]
    [InlineData("XLON", "USD", null)]
    [InlineData(null, null, null)]
    public void CanImportForTransaction_WhenOptionalVenueCurrencyOrSymbolIsMissing_ReturnsTrue(
        string? exchangeMic, string? tradingCurrency, string? providerSymbol)
    {
        var instrument = Instrument(
            exchangeMic: exchangeMic,
            tradingCurrency: tradingCurrency,
            providerSymbol: providerSymbol);

        Assert.True(CreateService().CanImportForTransaction(instrument));
    }

    [Fact]
    public async Task ValidateAndImportForTransaction_WhenAllProvidersFail_SavesAssetAndListingWithoutSymbols()
    {
        Asset? persistedAsset = null;
        AssetListing? persistedListing = null;
        _assetRepositoryMock.Setup(x => x.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _assetRepositoryMock.Setup(x => x.Upsert(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset asset, CancellationToken _) => { asset.Id = 10; persistedAsset = asset; return asset; });
        _listingRepositoryMock.Setup(x => x.Upsert(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetListing listing, CancellationToken _) => { listing.Id = 20; persistedListing = listing; return listing; });
        _avClientMock.Setup(x => x.GetDailySeries("CSPX.LON", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Alpha Vantage unavailable"));

        var service = CreateService();
        var validated = await service.ValidateForTransactionAsync(Instrument(), TestContext.Current.CancellationToken);
        var imported = await service.ImportValidatedAsync(validated, TestContext.Current.CancellationToken);

        Assert.Equal(10, imported.AssetId);
        Assert.Equal(20, imported.AssetListingId);
        Assert.Null(imported.MarketDataSymbolId);
        Assert.True(imported.CreatedAsset);
        Assert.True(imported.CreatedListing);
        Assert.False(imported.CreatedMarketDataSymbol);
        Assert.NotNull(persistedAsset);
        Assert.NotNull(persistedListing);
        _symbolRepositoryMock.Verify(x => x.Upsert(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportValidatedAsync_WhenVenueCurrencyAndSecurityTypeAreUnknown_SavesIncompleteAssetAndListing()
    {
        Asset? persistedAsset = null;
        AssetListing? persistedListing = null;
        _assetRepositoryMock.Setup(x => x.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _assetRepositoryMock.Setup(x => x.Upsert(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset asset, CancellationToken _) => { asset.Id = 11; persistedAsset = asset; return asset; });
        _listingRepositoryMock.Setup(x => x.Upsert(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetListing listing, CancellationToken _) => { listing.Id = 21; persistedListing = listing; return listing; });

        var incomplete = new InstrumentDiscoveryResultDto
        {
            Source = "OpenFIGI",
            DisplayName = "VANG FTSE AW USDA",
            Ticker = "VWCE",
            ExchangeMic = null,
            ExchangeCode = "GF",
            TradingCurrency = null,
            SecurityType = null,
            ListingFigi = "BBG000GF001"
        };

        var validated = new ValidatedInstrumentImport(
            incomplete,
            [new MarketDataSymbolResolution(MarketDataProvider.AlphaVantage, 100, "not_found", null, null, null, Error: "Symbol not found.")]);
        var imported = await CreateService().ImportValidatedAsync(validated, TestContext.Current.CancellationToken);

        Assert.Equal(11, imported.AssetId);
        Assert.Equal(21, imported.AssetListingId);
        Assert.Null(imported.MarketDataSymbolId);
        Assert.False(imported.CreatedMarketDataSymbol);
        Assert.Equal(string.Empty, imported.TradingCurrency);
        Assert.NotNull(persistedAsset);
        Assert.Equal(AssetType.Other, persistedAsset.Type);
        Assert.Null(persistedAsset.BaseCurrency);
        Assert.NotNull(persistedListing);
        Assert.Null(persistedListing.ExchangeMic);
        Assert.Null(persistedListing.TradingCurrency);
        Assert.Equal("GF", persistedListing.ExchangeName);
        Assert.Equal(1m, persistedListing.PriceMultiplier);
        Assert.True(persistedListing.IsIncomplete);
        _symbolRepositoryMock.Verify(x => x.Upsert(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportValidatedAsync_WhenOneProviderFailsAndAnotherResolves_PersistsOnlyResolvedSymbol()
    {
        _assetRepositoryMock.Setup(x => x.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _assetRepositoryMock.Setup(x => x.Upsert(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset asset, CancellationToken _) => { asset.Id = 12; return asset; });
        _listingRepositoryMock.Setup(x => x.Upsert(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetListing listing, CancellationToken _) => { listing.Id = 22; return listing; });
        _symbolRepositoryMock.Setup(x => x.Upsert(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketDataSymbol symbol, CancellationToken _) => { symbol.Id = 32; return symbol; });

        var validated = new ValidatedInstrumentImport(
            Instrument(providerSymbol: "CSPX.LON"),
            [
                new MarketDataSymbolResolution(MarketDataProvider.AlphaVantage, 100, "unavailable", "CSPX.LON", "LON", "USD", Error: "Quota exceeded"),
                new MarketDataSymbolResolution(MarketDataProvider.TwelveData, 200, "resolved", "CSPX:LSE", "LSE", "USD")
            ]);

        var imported = await CreateService().ImportValidatedAsync(validated, TestContext.Current.CancellationToken);

        Assert.True(imported.CreatedMarketDataSymbol);
        Assert.Equal(32, imported.MarketDataSymbolId);
        _symbolRepositoryMock.Verify(
            x => x.Upsert(
                It.Is<MarketDataSymbol>(s => s.Provider == MarketDataProvider.TwelveData && s.Symbol == "CSPX:LSE" && s.IsEnabled && s.IsPrimary),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _symbolRepositoryMock.Verify(
            x => x.Upsert(
                It.Is<MarketDataSymbol>(s => s.Provider == MarketDataProvider.AlphaVantage),
                It.IsAny<CancellationToken>()),
            Times.Never);
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

        var result = await CreateService().ImportValidatedAsync(new(Instrument("ETP"), [new(MarketDataProvider.AlphaVantage, 100, "resolved", "CSPX.LON", "LON", "USD")]), TestContext.Current.CancellationToken);

        Assert.True(result.CreatedMarketDataSymbol);
        Assert.Equal(AssetType.ETF, persistedAsset?.Type);
        Assert.NotNull(persistedSymbol);
        Assert.True(persistedSymbol.IsEnabled);
        Assert.NotNull(persistedSymbol.LastValidatedAt);
        Assert.Null(persistedSymbol.LastError);
        _avClientMock.Verify(x => x.GetDailySeries(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WithoutAlphaVantageSymbol_CreatesOtherProviderAndKeepsAdministratorValues()
    {
        var asset = new Asset { Id = 1, Name = "Administrator name", Type = AssetType.ETF };
        var listing = new AssetListing { Id = 2, AssetId = 1, Ticker = "CSPX", ExchangeMic = "XLON", ExchangeName = "Curated exchange", TradingCurrency = "USD" };
        _assetRepositoryMock.Setup(x => x.GetByShareClassFigi("BBGSC", It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _listingRepositoryMock.Setup(x => x.GetByAsset(1, It.IsAny<CancellationToken>())).ReturnsAsync([listing]);
        _symbolRepositoryMock.Setup(x => x.Upsert(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketDataSymbol symbol, CancellationToken _) => symbol);
        var resolver = new Mock<IMarketDataSymbolResolver>();
        resolver.SetupGet(x => x.Provider).Returns(MarketDataProvider.TwelveData);
        resolver.SetupGet(x => x.Priority).Returns(200);
        resolver.Setup(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketDataSymbolResolution(MarketDataProvider.TwelveData, 200, "candidate", "CSPX:LSE", "LSE", "USD", "Provider name", "Stock", "London"));
        resolver.Setup(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var service = new InstrumentImportService(new([resolver.Object]), _assetRepositoryMock.Object,
            _listingRepositoryMock.Object, _symbolRepositoryMock.Object, _logger);
        var instrument = Instrument(providerSymbol: null);
        Assert.True(service.CanImportForTransaction(instrument));
        var validated = await service.ValidateForTransactionAsync(instrument, TestContext.Current.CancellationToken);
        var result = await service.ImportValidatedAsync(validated, TestContext.Current.CancellationToken);
        Assert.True(result.CreatedMarketDataSymbol);
        Assert.Equal("resolved", Assert.Single(result.ProviderResults).Status);
        Assert.Equal("Administrator name", asset.Name);
        Assert.Equal(AssetType.ETF, asset.Type);
        Assert.Equal("Curated exchange", listing.ExchangeName);
        _symbolRepositoryMock.Verify(x => x.Upsert(It.Is<MarketDataSymbol>(x => x.Provider == MarketDataProvider.TwelveData && x.IsEnabled && x.IsPrimary && x.LastValidatedAt != null), It.IsAny<CancellationToken>()), Times.Once);
        resolver.Verify(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()), Times.Once);
        resolver.Verify(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Earlier validation failed")]
    [InlineData(null)]
    public async Task ImportValidatedAsync_UpdatesDiagnosticsAndPreservesDisabledMappings(string? previousError)
    {
        var asset = new Asset { Id = 1, Name = "Existing asset", ShareClassFigi = "BBGSC", Type = AssetType.ETF };
        var listing = new AssetListing { Id = 2, AssetId = 1, Ticker = "CSPX", ExchangeMic = "XLON", ExchangeName = "London", TradingCurrency = "USD" };
        var symbol = new MarketDataSymbol
        {
            Id = 3,
            AssetListingId = 2,
            Provider = MarketDataProvider.TwelveData,
            Symbol = "CSPX:LSE",
            IsEnabled = false,
            LastError = previousError
        };
        _assetRepositoryMock.Setup(x => x.GetByShareClassFigi("BBGSC", It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _listingRepositoryMock.Setup(x => x.GetByAsset(1, It.IsAny<CancellationToken>())).ReturnsAsync([listing]);
        _symbolRepositoryMock.Setup(x => x.GetByListing(2, It.IsAny<CancellationToken>())).ReturnsAsync([symbol]);
        var validated = new ValidatedInstrumentImport(Instrument(providerSymbol: null),
            [new(MarketDataProvider.TwelveData, 200, "resolved", "CSPX:LSE", "LSE", "USD")]);
        await CreateService().ImportValidatedAsync(validated, TestContext.Current.CancellationToken);
        Assert.False(symbol.IsEnabled);
        Assert.False(symbol.IsPrimary);
        Assert.NotNull(symbol.LastValidatedAt);
        Assert.Null(symbol.LastError);
        _symbolRepositoryMock.Verify(x => x.Update(symbol, It.IsAny<CancellationToken>()), Times.Once);
        _symbolRepositoryMock.Verify(x => x.Upsert(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class TestResolver(IAlphaVantageClient client) : IMarketDataSymbolResolver
    {
        public MarketDataProvider Provider => MarketDataProvider.AlphaVantage;
        public int Priority => 100;
        public Task<MarketDataSymbolResolution> ResolveAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default) =>
            Task.FromResult(new MarketDataSymbolResolution(Provider, Priority, "candidate", instrument.ProviderSymbol, instrument.ExchangeCode, instrument.TradingCurrency));
        public async Task<bool> ValidateAsync(MarketDataSymbolResolution candidate, CancellationToken ct = default) =>
            (await client.GetDailySeries(candidate.Symbol!, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow,
                new Currency { ShortName = candidate.Currency! }, ct)).Any(x => x.PricePerUnit > 0);
    }
}