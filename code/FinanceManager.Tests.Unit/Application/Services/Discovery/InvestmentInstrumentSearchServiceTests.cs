using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Discovery;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentInstrumentSearchServiceTests
{
    private readonly Mock<IAssetListingRepository> _listingRepository = new();
    private readonly Mock<IInvestmentInstrumentDiscoveryService> _discoveryService = new();
    private readonly Mock<IInstrumentImportService> _importService = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private InvestmentInstrumentSearchService CreateService() =>
        new(_listingRepository.Object, _discoveryService.Object, _importService.Object, _cache);

    [Fact]
    public async Task SearchAsync_ReturnsLocalFirstAndDeduplicatesExternalListings()
    {
        var local = new AssetListing
        {
            Id = 7,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD",
            ListingFigi = "BBG001",
            Asset = new Asset { Name = "iShares Core S&P 500 UCITS ETF", Isin = "IE00B5BMR087" }
        };
        var duplicate = new InstrumentDiscoveryResultDto
        {
            Ticker = "CSPX",
            DisplayName = "iShares Core S&P 500 UCITS ETF",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD",
            ListingFigi = "BBG001",
            ConfidenceScore = 1m
        };
        var external = new InstrumentDiscoveryResultDto
        {
            Ticker = "CSPX",
            DisplayName = "iShares Core S&P 500 UCITS ETF",
            ExchangeMic = "XETR",
            ExchangeName = "Xetra",
            TradingCurrency = "EUR",
            ListingFigi = "BBG002",
            ConfidenceScore = 0.8m
        };
        _listingRepository.Setup(x => x.SearchAsync("CSPX", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([local]);
        _discoveryService.Setup(x => x.SearchAsync("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate, external]);
        _importService.Setup(x => x.CanImportForTransaction(It.IsAny<InstrumentDiscoveryResultDto>())).Returns(true);

        var results = await CreateService()
            .SearchAsync("CSPX", 20, TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Equal(InstrumentOptionSource.Existing, results[0].Source);
        Assert.Equal(InstrumentOptionSource.External, results[1].Source);
        Assert.Equal("XETR", results[1].ExchangeMic);
        Assert.NotEmpty(results[1].ResultId);
        Assert.Equal(external, await CreateService()
            .ResolveExternalResultAsync(results[1].ResultId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_HidesExternalResultsThatCannotBeImported()
    {
        var notImportable = new InstrumentDiscoveryResultDto
        {
            Ticker = "VWCE",
            DisplayName = "Vanguard FTSE All-World UCITS ETF",
            ExchangeMic = "XETR",
            ExchangeName = "Xetra",
            TradingCurrency = "EUR",
            ConfidenceScore = 1m
        };
        var importable = new InstrumentDiscoveryResultDto
        {
            Ticker = "VWCE",
            DisplayName = "Vanguard FTSE All-World UCITS ETF",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD",
            ProviderSymbol = "VWCE.LON",
            SecurityType = "ETF",
            ConfidenceScore = 0.9m
        };
        _listingRepository.Setup(x => x.SearchAsync("VWCE", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _discoveryService.Setup(x => x.SearchAsync("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync([notImportable, importable]);
        _importService.Setup(x => x.CanImportForTransaction(notImportable)).Returns(false);
        _importService.Setup(x => x.CanImportForTransaction(importable)).Returns(true);

        var results = await CreateService()
            .SearchAsync("VWCE", 20, TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(InstrumentOptionSource.External, result.Source);
        Assert.Equal("XLON", result.ExchangeMic);
        _importService.Verify(x => x.CanImportForTransaction(notImportable), Times.Once);
        _importService.Verify(x => x.CanImportForTransaction(importable), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_WithPlainVwceQuery_OffersOnlyTheImportableXetraResult()
    {
        // Mirrors the plain "VWCE" discovery output: the German composite resolves to Xetra with an
        // Alpha Vantage symbol, while the other German venue codes have no MIC/currency/symbol. The
        // real import rules must hide the latter because they can never be saved.
        var xetra = new InstrumentDiscoveryResultDto
        {
            Source = "Combined",
            DisplayName = "VANG FTSE AW USDA",
            Ticker = "VWCE",
            ExchangeMic = "XETR",
            ExchangeCode = "GR",
            ExchangeName = "Xetra",
            TradingCurrency = "EUR",
            ProviderSymbol = "VWCE.DEX",
            SecurityType = "ETF",
            ConfidenceScore = 1m
        };
        var unknownVenues = new[] { "GF", "GD", "GS", "LA" }
            .Select(code => new InstrumentDiscoveryResultDto
            {
                Source = "OpenFIGI",
                DisplayName = "VANG FTSE AW USDA",
                Ticker = "VWCE",
                ExchangeCode = code,
                ConfidenceScore = 0.7m
            })
            .ToList();

        _listingRepository.Setup(x => x.SearchAsync("VWCE", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _discoveryService.Setup(x => x.SearchAsync("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. unknownVenues, xetra]);

        var importService = new InstrumentImportService(
            new MarketDataSymbolResolutionService([]),
            Mock.Of<IAssetRepository>(),
            Mock.Of<IAssetListingRepository>(),
            Mock.Of<IMarketDataSymbolRepository>(),
            LoggerFactory.Create(_ => { }).CreateLogger<InstrumentImportService>());
        var service = new InvestmentInstrumentSearchService(
            _listingRepository.Object, _discoveryService.Object, importService, _cache);

        var results = await service.SearchAsync("VWCE", 20, TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(InstrumentOptionSource.External, result.Source);
        Assert.Equal("XETR", result.ExchangeMic);
        Assert.Equal("EUR", result.TradingCurrency);
        Assert.Equal("VWCE.DEX", result.ExternalInstrument?.ProviderSymbol);
    }
}