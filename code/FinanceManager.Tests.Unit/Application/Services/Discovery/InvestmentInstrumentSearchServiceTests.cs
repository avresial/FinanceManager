using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Discovery;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentInstrumentSearchServiceTests
{
    private readonly Mock<IAssetListingRepository> _listingRepository = new();
    private readonly Mock<IInvestmentInstrumentDiscoveryService> _discoveryService = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

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

        var results = await new InvestmentInstrumentSearchService(
            _listingRepository.Object, _discoveryService.Object, _cache)
            .SearchAsync("CSPX", 20, TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Equal(InstrumentOptionSource.Existing, results[0].Source);
        Assert.Equal(InstrumentOptionSource.External, results[1].Source);
        Assert.Equal("XETR", results[1].ExchangeMic);
        Assert.NotEmpty(results[1].ResultId);
        Assert.Equal(external, await new InvestmentInstrumentSearchService(
            _listingRepository.Object, _discoveryService.Object, _cache)
            .ResolveExternalResultAsync(results[1].ResultId, TestContext.Current.CancellationToken));
    }
}