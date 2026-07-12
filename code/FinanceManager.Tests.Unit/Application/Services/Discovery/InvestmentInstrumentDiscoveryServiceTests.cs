using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Discovery;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentInstrumentDiscoveryServiceTests
{
    private readonly Mock<IOpenFigiClient> _openFigiClientMock = new();
    private readonly Mock<IAlphaVantageClient> _avClientMock = new();
    private readonly Mock<IAssetRepository> _assetRepositoryMock = new();
    private readonly Mock<IAssetListingRepository> _listingRepositoryMock = new();
    private readonly Mock<IMarketDataSymbolRepository> _symbolRepositoryMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly ILogger<InvestmentInstrumentDiscoveryService> _logger =
        LoggerFactory.Create(_ => { }).CreateLogger<InvestmentInstrumentDiscoveryService>();

    private InvestmentInstrumentDiscoveryService CreateService() =>
        new(_openFigiClientMock.Object, _avClientMock.Object, _assetRepositoryMock.Object,
            _listingRepositoryMock.Object, _symbolRepositoryMock.Object, _cache, _logger);

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_WithNullOrWhitespaceQuery_ReturnsEmpty(string? query)
    {
        var service = CreateService();

        var results = await service.SearchAsync(query!, TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_WithIsin_ReturnsListingLevelOpenFigiResults()
    {
        _openFigiClientMock
            .Setup(x => x.MapByIsinAsync("IE00B5BMR087", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Currency: "USD", Figi: "BBG001", CompositeFigi: "BBGC", ShareClassFigi: "BBGSC"),
                new(Isin: null, Ticker: "SXR8", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "GY",
                    Currency: "EUR", Figi: "BBG002", CompositeFigi: "BBGC", ShareClassFigi: "BBGSC"),
            });

        var service = CreateService();

        var results = await service.SearchAsync("IE00B5BMR087", TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("OpenFIGI", r.Source));
        Assert.All(results, r => Assert.Equal("IE00B5BMR087", r.Isin));
        Assert.Contains(results, r => r.ExchangeMic == "XLON" && r.TradingCurrency == "USD");
        Assert.Contains(results, r => r.ExchangeMic == "XETR" && r.TradingCurrency == "EUR");
        // No Alpha Vantage symbol search is performed on the ISIN path.
        _avClientMock.Verify(x => x.SearchTicker(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithTicker_MergesOpenFigiAndAlphaVantage_IntoCombinedResult()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Currency: "USD", Figi: "BBG001", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "CSPX.LON", Name = "iShares Core S&P 500 ETF", Type = "ETF", Region = "United Kingdom", Currency = "USD", MatchScore = 1m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("CSPX", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("Combined", result.Source);
        Assert.Equal("CSPX.LON", result.ProviderSymbol);
        Assert.Equal("XLON", result.ExchangeMic);
        Assert.Equal(1.0m, result.ConfidenceScore);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task SearchAsync_WithOpenFigiOnly_NormalizesAndWarnsAboutMissingProviderSymbol()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Currency: "USD", Figi: "BBG001", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        var service = CreateService();

        var results = await service.SearchAsync("CSPX", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("OpenFIGI", result.Source);
        Assert.Null(result.ProviderSymbol);
        Assert.Equal(0.7m, result.ConfidenceScore);
        Assert.Contains(result.Warnings, w => w.Contains("price symbol", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_WithAlphaVantageOnly_ReturnsLowConfidenceUnconfirmedResult()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>());
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "CSPX.LON", Name = "iShares Core S&P 500 ETF", Type = "ETF", Region = "United Kingdom", Currency = "USD", MatchScore = 0.9m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("CSPX", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("AlphaVantage", result.Source);
        // The raw provider symbol stays on ProviderSymbol; the display ticker is normalized to the
        // base ticker and the venue identity is resolved from the AV region.
        Assert.Equal("CSPX.LON", result.ProviderSymbol);
        Assert.Equal("CSPX", result.Ticker);
        Assert.Equal("XLON", result.ExchangeMic);
        Assert.True(result.ConfidenceScore <= 0.6m);
        Assert.Contains(result.Warnings, w => w.Contains("OpenFIGI", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_DoesNotAttachSameProviderSymbolToMultipleListings()
    {
        // Two distinct venues for the same ticker, both quoted in USD, but only one Alpha Vantage symbol.
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Currency: "USD", Figi: "BBG001", ShareClassFigi: "BBGSC"),
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "GY",
                    Currency: "USD", Figi: "BBG002", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "CSPX.LON", Name = "iShares Core S&P 500 ETF", Type = "ETF", Region = "United Kingdom", Currency = "USD", MatchScore = 1m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("CSPX", TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        // The single AV symbol must be attached to exactly one listing, not both.
        Assert.Single(results, r => r.ProviderSymbol == "CSPX.LON");
        Assert.Single(results, r => r.Source == "Combined");
        Assert.Single(results, r => r.Source == "OpenFIGI" && r.ProviderSymbol is null);
    }

    [Fact]
    public async Task SearchAsync_WithGbxListing_AddsPriceMultiplierWarning()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VUKE", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "VUKE", Name: "Vanguard FTSE 100 UCITS ETF", ExchCode: "LN",
                    Currency: "GBX", Figi: "BBG010", ShareClassFigi: "BBGSC10"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("VUKE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        var service = CreateService();

        var results = await service.SearchAsync("VUKE", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Contains(result.Warnings, w => w.Contains("minor units", StringComparison.OrdinalIgnoreCase) && w.Contains("0.01"));
    }

    [Fact]
    public async Task SearchAsync_WithUnknownExchange_AddsAmbiguousMappingWarningAndNullMic()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "ZZ",
                    Currency: "USD", Figi: "BBG001", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        var service = CreateService();

        var results = await service.SearchAsync("CSPX", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Null(result.ExchangeMic);
        Assert.Contains(result.Warnings, w => w.Contains("ambiguous", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_CachesResults_AndDoesNotCallProvidersTwice()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Currency: "USD", Figi: "BBG001", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        var service = CreateService();

        await service.SearchAsync("CSPX", TestContext.Current.CancellationToken);
        await service.SearchAsync("cspx", TestContext.Current.CancellationToken);

        _openFigiClientMock.Verify(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

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