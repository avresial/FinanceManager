using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.Assets.Discovery;
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
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly ILogger<InvestmentInstrumentDiscoveryService> _logger =
        LoggerFactory.Create(_ => { }).CreateLogger<InvestmentInstrumentDiscoveryService>();
    private InvestmentInstrumentDiscoveryService CreateService() =>
        new(_openFigiClientMock.Object, _avClientMock.Object, _cache, _logger);

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
    public async Task SearchAsync_WithIsin_CorrelatesAlphaVantageSymbolsAndPreservesIsin()
    {
        // Real OpenFIGI order for IE00BK5BQT80: two London tickers before the German composite.
        // Selecting the first N distinct tickers would spend the whole budget on London and leave
        // Xetra's VWCE without a symbol, so the selection takes one ticker per venue.
        _openFigiClientMock
            .Setup(x => x.MapByIsinAsync("IE00BK5BQT80", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "VWRA", Name: "Vanguard FTSE All-World UCITS ETF", ExchCode: "LN",
                    Figi: "BBG001", CompositeFigi: "BBGC", ShareClassFigi: "BBGSC"),
                new(Isin: null, Ticker: "VWRP", Name: "Vanguard FTSE All-World UCITS ETF", ExchCode: "LN",
                    Figi: "BBG002", CompositeFigi: "BBGC", ShareClassFigi: "BBGSC"),
                new(Isin: null, Ticker: "VWCE", Name: "Vanguard FTSE All-World UCITS ETF", ExchCode: "GR",
                    Figi: "BBG003", CompositeFigi: "BBGC", ShareClassFigi: "BBGSC"),
                new(Isin: null, Ticker: "VWCE", Name: "Vanguard FTSE All-World UCITS ETF", ExchCode: "GF",
                    Figi: "BBG004", CompositeFigi: "BBGC", ShareClassFigi: "BBGSC"),
                new(Isin: null, Ticker: "VWCE", Name: "Vanguard FTSE All-World UCITS ETF", ExchCode: "LA",
                    Figi: "BBG005", CompositeFigi: "BBGC", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("VWRA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "VWRA.LON", Name = "Vanguard FTSE All-World UCITS ETF", Type = "ETF", Region = "United Kingdom", Currency = "USD", MatchScore = 1m },
            });
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "VWCE.DEX", Name = "Vanguard FTSE All-World UCITS ETF", Type = "ETF", Region = "XETRA", Currency = "EUR", MatchScore = 1m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("IE00BK5BQT80", TestContext.Current.CancellationToken);

        Assert.Equal(5, results.Count);
        Assert.All(results, r => Assert.Equal("IE00BK5BQT80", r.Isin));
        Assert.Contains(results, r => r.ExchangeMic == "XLON" && r.Ticker == "VWRA"
            && r.TradingCurrency == "USD" && r.ProviderSymbol == "VWRA.LON");
        var xetra = Assert.Single(results, r => r.ExchangeMic == "XETR");
        Assert.Equal("VWCE", xetra.Ticker);
        Assert.Equal("EUR", xetra.TradingCurrency);
        Assert.Equal("VWCE.DEX", xetra.ProviderSymbol);
        Assert.Equal("IE00BK5BQT80", xetra.Isin);
        // One ticker per distinct venue, so the second London ticker is not searched; the lookup
        // budget is capped to protect the Alpha Vantage free-tier quota.
        _avClientMock.Verify(x => x.SearchTicker("VWRA", It.IsAny<CancellationToken>()), Times.Once);
        _avClientMock.Verify(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()), Times.Once);
        _avClientMock.Verify(x => x.SearchTicker("VWRP", It.IsAny<CancellationToken>()), Times.Never);
        _avClientMock.Verify(x => x.SearchTicker(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SearchAsync_WithIsin_LimitsAlphaVantageLookupsToThreeVenues()
    {
        _openFigiClientMock
            .Setup(x => x.MapByIsinAsync("IE00BK5BQT80", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "AAA", Name: "A", ExchCode: "GY", Figi: "BBG001", ShareClassFigi: "BBGSC1"),
                new(Isin: null, Ticker: "BBB", Name: "B", ExchCode: "LN", Figi: "BBG002", ShareClassFigi: "BBGSC2"),
                new(Isin: null, Ticker: "CCC", Name: "C", ExchCode: "WA", Figi: "BBG003", ShareClassFigi: "BBGSC3"),
                new(Isin: null, Ticker: "DDD", Name: "D", ExchCode: "TT", Figi: "BBG004", ShareClassFigi: "BBGSC4"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        var service = CreateService();

        var results = await service.SearchAsync("IE00BK5BQT80", TestContext.Current.CancellationToken);

        Assert.Equal(4, results.Count);
        _avClientMock.Verify(x => x.SearchTicker(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _avClientMock.Verify(x => x.SearchTicker("DDD", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithIsin_WhenAlphaVantageFails_ReturnsOpenFigiOnlyResults()
    {
        _openFigiClientMock
            .Setup(x => x.MapByIsinAsync("IE00BK5BQT80", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "VWCE", Name: "Vanguard FTSE All-World UCITS ETF", ExchCode: "GY",
                    Figi: "BBG002", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Alpha Vantage unavailable"));

        var service = CreateService();

        var results = await service.SearchAsync("IE00BK5BQT80", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("OpenFIGI", result.Source);
        Assert.Null(result.ProviderSymbol);
        Assert.Equal("IE00BK5BQT80", result.Isin);
        Assert.Equal("XETR", result.ExchangeMic);
        Assert.Equal("EUR", result.TradingCurrency);
    }

    [Fact]
    public async Task SearchAsync_WithIsin_DropsAlphaVantageMatchesThatCorrelateToNoListing()
    {
        _openFigiClientMock
            .Setup(x => x.MapByIsinAsync("IE00BK5BQT80", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "VWCE", Name: "Vanguard FTSE All-World UCITS ETF", ExchCode: "GY",
                    Figi: "BBG002", ShareClassFigi: "BBGSC"),
            });
        // Alpha Vantage's symbol search is fuzzy; this London match correlates to no OpenFIGI listing
        // for the ISIN, so it may be a different security and must not inherit the queried ISIN.
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "VWCE.LON", Name = "Vanguard FTSE All-World UCITS ETF", Type = "ETF", Region = "United Kingdom", Currency = "USD", MatchScore = 1m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("IE00BK5BQT80", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("OpenFIGI", result.Source);
        Assert.Null(result.ProviderSymbol);
        Assert.Equal("IE00BK5BQT80", result.Isin);
        Assert.DoesNotContain(results, r => r.Source == "AlphaVantage");
        Assert.DoesNotContain(results, r => r.ProviderSymbol == "VWCE.LON");
    }

    [Fact]
    public async Task SearchAsync_WithIsin_DoesNotCorrelateFuzzyTickerMatchFromAnotherSecurity()
    {
        // AV's fuzzy search for "ABC" returns "ABCD.DEX". Attaching it would stamp another
        // security's prices (and the queried ISIN) onto the ABC listing.
        _openFigiClientMock
            .Setup(x => x.MapByIsinAsync("IE00BK5BQT80", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "ABC", Name: "ABC Corporation", ExchCode: "GY",
                    Figi: "BBG002", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("ABC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "ABCD.DEX", Name = "ABCD Corporation", Type = "Stock", Region = "XETRA", Currency = "EUR", MatchScore = 0.8m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("IE00BK5BQT80", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("ABC", result.Ticker);
        Assert.Equal("OpenFIGI", result.Source);
        Assert.Null(result.ProviderSymbol);
        Assert.Equal("IE00BK5BQT80", result.Isin);
        Assert.DoesNotContain(results, r => r.ProviderSymbol == "ABCD.DEX");
    }

    [Fact]
    public async Task SearchAsync_WithTicker_MergesOpenFigiAndAlphaVantage_IntoCombinedResult()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Figi: "BBG001", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
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
        Assert.Equal("USD", result.TradingCurrency);
        Assert.Equal("ETF", result.SecurityType);
        Assert.Equal(1.0m, result.ConfidenceScore);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task SearchAsync_WhenAlphaVantageReturnsFuzzyTickerMatch_DoesNotAttachProviderSymbol()
    {
        // Searching "ABC" makes Alpha Vantage return "ABCD.DEX"; matching on region alone would
        // attach another security's prices to the OpenFIGI ABC listing.
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("ABC", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "ABC", Name: "ABC Corporation", ExchCode: "GY",
                    Figi: "BBG001", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("ABC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "ABCD.DEX", Name = "ABCD Corporation", Type = "Stock", Region = "XETRA", Currency = "EUR", MatchScore = 0.8m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("ABC", TestContext.Current.CancellationToken);

        var openFigiResult = Assert.Single(results, r => r.Source == "OpenFIGI");
        Assert.Equal("ABC", openFigiResult.Ticker);
        Assert.Null(openFigiResult.ProviderSymbol);
        Assert.DoesNotContain(results, r => r.Source == "Combined");
        Assert.Contains(results, r => r.Source == "AlphaVantage" && r.ProviderSymbol == "ABCD.DEX");
    }

    [Fact]
    public async Task SearchAsync_WithBrokerSuffix_WhenFilteredLookupIsEmpty_RetriesUnfilteredAndUsesHintedVenue()
    {
        // Regression for VWCE.DE: OpenFIGI has no GY listing, only the German composite "GR" and
        // other German venue codes; the unfiltered retry must map GR back to Xetra.
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VWCE", "GY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>());
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VWCE", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GR",
                    Figi: "BBG001", ShareClassFigi: "BBGSC", SecurityType: "ETP", MarketSector: "Equity"),
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GF",
                    Figi: "BBG002", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GD",
                    Figi: "BBG003", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "LN",
                    Figi: "BBG004", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "VWCE.DEX", Name = "Vanguard FTSE All-World UCITS ETF", Type = "ETF", Region = "XETRA", Currency = "EUR", MatchScore = 1m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("VWCE.DE", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("VWCE", result.Ticker);
        Assert.Equal("XETR", result.ExchangeMic);
        Assert.Equal("Xetra", result.ExchangeName);
        Assert.Equal("EUR", result.TradingCurrency);
        Assert.Equal("VWCE.DEX", result.ProviderSymbol);
        Assert.Equal("ETF", result.SecurityType);
        Assert.Equal("Combined", result.Source);
        _openFigiClientMock.Verify(
            x => x.MapByTickerAsync("VWCE", "GY", It.IsAny<CancellationToken>()), Times.Once);
        _openFigiClientMock.Verify(
            x => x.MapByTickerAsync("VWCE", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_WithPlainVwceQuery_ResolvesGermanCompositeAndLeavesUnknownVenuesUnconfirmed()
    {
        // Regression for a plain "VWCE" search: OpenFIGI knows only the German composite "GR" (no
        // "GY"), so the composite must resolve to Xetra while the other German venue codes stay
        // visible with no MIC instead of being silently dropped.
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VWCE", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GR",
                    Figi: "BBG001", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GF",
                    Figi: "BBG002", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GD",
                    Figi: "BBG003", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GS",
                    Figi: "BBG004", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "LA",
                    Figi: "BBG005", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "VWCE.DEX", Name = "Vanguard FTSE All-World UCITS ETF", Type = "ETF", Region = "XETRA", Currency = "EUR", MatchScore = 1m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("VWCE", TestContext.Current.CancellationToken);

        Assert.Equal(5, results.Count);
        var xetra = Assert.Single(results, r => r.ExchangeMic == "XETR");
        Assert.Equal("EUR", xetra.TradingCurrency);
        Assert.Equal("VWCE.DEX", xetra.ProviderSymbol);
        Assert.Equal("Combined", xetra.Source);
        Assert.Equal(4, results.Count(r => r.ExchangeMic is null));
        Assert.All(results.Where(r => r.ExchangeMic is null), r => Assert.Null(r.ProviderSymbol));
    }

    [Fact]
    public async Task SearchAsync_WhenPrimaryVenueListingExists_DropsCompositeListing()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VWCE", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GY",
                    Figi: "BBG001", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GR",
                    Figi: "BBG002", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        var service = CreateService();

        var results = await service.SearchAsync("VWCE", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("GY", result.ExchangeCode);
        Assert.Equal("XETR", result.ExchangeMic);
        Assert.Equal("EUR", result.TradingCurrency);
    }

    [Fact]
    public async Task SearchAsync_WhenAlphaVantageHasNoMatch_FallsBackToVenueDefaultCurrency()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "GY",
                    Figi: "BBG001", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Figi: "BBG002", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        var service = CreateService();

        var results = await service.SearchAsync("CSPX", TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Equal("EUR", Assert.Single(results, r => r.ExchangeMic == "XETR").TradingCurrency);
        // London trades GBX/GBP/USD, so there is no safe default currency.
        Assert.Null(Assert.Single(results, r => r.ExchangeMic == "XLON").TradingCurrency);
    }

    [Fact]
    public async Task SearchAsync_WhenAlphaVantageHasNoType_UsesOpenFigiSecurityType()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VWCE", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GY",
                    Figi: "BBG001", ShareClassFigi: "BBGSC", SecurityType: "ETP", SecurityType2: "Mutual Fund", MarketSector: "Equity"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "VWCE.DEX", Name = "Vanguard FTSE All-World UCITS ETF", Type = string.Empty, Region = "XETRA", Currency = string.Empty, MatchScore = 1m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("VWCE", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("ETP", result.SecurityType);
        Assert.Equal("Equity", result.MarketSector);
        Assert.Equal("EUR", result.TradingCurrency);
        Assert.Equal("VWCE.DEX", result.ProviderSymbol);
    }

    [Fact]
    public async Task SearchAsync_WithOpenFigiOnly_NormalizesAndWarnsAboutMissingProviderSymbol()
    {
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Figi: "BBG001", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        var service = CreateService();

        var results = await service.SearchAsync("CSPX", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("OpenFIGI", result.Source);
        Assert.Null(result.ProviderSymbol);
        Assert.Null(result.TradingCurrency);
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
        Assert.Equal("USD", result.TradingCurrency);
        Assert.Equal("ETF", result.SecurityType);
        Assert.True(result.ConfidenceScore <= 0.6m);
        Assert.Null(result.Isin);
        Assert.Contains(result.Warnings, w => w == "Identity was not confirmed by OpenFIGI; check the exchange and currency before saving.");
    }

    [Fact]
    public async Task SearchAsync_WithNameQuery_WhenOnlyAlphaVantageMatches_DerivesTickerFromProviderSymbol()
    {
        // A one-word name search like "TESCO" must not persist the search text as the ticker; the
        // AV symbol's base ticker identifies the instrument's listing.
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("TESCO", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>());
        _avClientMock
            .Setup(x => x.SearchTicker("TESCO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "TSCO.LON", Name = "Tesco PLC", Type = "Stock", Region = "United Kingdom", Currency = "GBP", MatchScore = 0.8m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("TESCO", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("AlphaVantage", result.Source);
        Assert.Equal("TSCO", result.Ticker);
        Assert.Equal("TSCO.LON", result.ProviderSymbol);
        Assert.Equal("XLON", result.ExchangeMic);
    }

    [Fact]
    public async Task SearchAsync_WithMultipleAlphaVantageMatchesOnOneVenue_KeepsDistinctTickers()
    {
        // Deriving the ticker from the AV symbol keeps two same-venue matches distinct; using the
        // search text for both would collapse them into one result.
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VWCE", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>());
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "VWCE.DEX", Name = "Vanguard FTSE All-World UCITS ETF", Type = "ETF", Region = "XETRA", Currency = "EUR", MatchScore = 1m },
                new() { Symbol = "VWCA.DEX", Name = "Vanguard FTSE All-World UCITS ETF Acc", Type = "ETF", Region = "XETRA", Currency = "EUR", MatchScore = 0.9m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("VWCE", TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Ticker == "VWCE" && r.ProviderSymbol == "VWCE.DEX" && r.ExchangeMic == "XETR");
        Assert.Contains(results, r => r.Ticker == "VWCA" && r.ProviderSymbol == "VWCA.DEX" && r.ExchangeMic == "XETR");
    }

    [Fact]
    public async Task SearchAsync_WhenAlphaVantageRegionIsFrankfurt_DoesNotResolveToXetra()
    {
        // Frankfurt (XFRA) and Xetra (XETR) are different venues; .FRK symbols must not be treated
        // as Xetra listings.
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VWCE", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>());
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "VWCE.FRK", Name = "Vanguard FTSE All-World UCITS ETF", Type = "ETF", Region = "Frankfurt", Currency = "EUR", MatchScore = 0.9m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("VWCE", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("VWCE", result.Ticker);
        Assert.Equal("VWCE.FRK", result.ProviderSymbol);
        Assert.Null(result.ExchangeMic);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchAsync_WithXetraListingAndFrankfurtAlphaVantageMatch_AttachesXetraSymbol(bool frankfurtFirst)
    {
        // Both AV symbols correlate by ticker, but only the XETRA one belongs to the Xetra listing;
        // it must win regardless of the order AV returns them in.
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VWCE", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GR",
                    Figi: "BBG001", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
                new(Isin: null, Ticker: "VWCE", Name: "VANG FTSE AW USDA", ExchCode: "GY",
                    Figi: "BBG002", ShareClassFigi: "BBGSC", SecurityType: "ETP"),
            });
        var frankfurt = new TickerSearchMatch
        {
            Symbol = "VWCE.FRK",
            Name = "Vanguard FTSE All-World UCITS ETF",
            Type = "ETF",
            Region = "Frankfurt",
            Currency = "EUR",
            MatchScore = 0.9m,
        };
        var xetra = new TickerSearchMatch
        {
            Symbol = "VWCE.DEX",
            Name = "Vanguard FTSE All-World UCITS ETF",
            Type = "ETF",
            Region = "XETRA",
            Currency = "EUR",
            MatchScore = 1m,
        };
        _avClientMock
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(frankfurtFirst ? [frankfurt, xetra] : [xetra, frankfurt]);

        var service = CreateService();

        var results = await service.SearchAsync("VWCE", TestContext.Current.CancellationToken);

        var xetraResult = Assert.Single(results, r => r.ExchangeMic == "XETR");
        Assert.Equal("VWCE.DEX", xetraResult.ProviderSymbol);
        Assert.Equal("Combined", xetraResult.Source);
        Assert.DoesNotContain(results, r => r.ExchangeMic == "XETR" && r.ProviderSymbol == "VWCE.FRK");
    }

    [Fact]
    public async Task SearchAsync_DoesNotAttachSameProviderSymbolToMultipleListings()
    {
        // Two distinct venues for the same ticker, but only one Alpha Vantage symbol.
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "LN",
                    Figi: "BBG001", ShareClassFigi: "BBGSC"),
                new(Isin: null, Ticker: "CSPX", Name: "iShares Core S&P 500 UCITS ETF", ExchCode: "GY",
                    Figi: "BBG002", ShareClassFigi: "BBGSC"),
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
                    Figi: "BBG010", ShareClassFigi: "BBGSC10"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("VUKE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "VUKE.LON", Name = "Vanguard FTSE 100 UCITS ETF", Type = "ETF", Region = "United Kingdom", Currency = "GBX", MatchScore = 1m },
            });

        var service = CreateService();

        var results = await service.SearchAsync("VUKE", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("GBX", result.TradingCurrency);
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
                    Figi: "BBG001", ShareClassFigi: "BBGSC"),
            });
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        var service = CreateService();

        var results = await service.SearchAsync("CSPX", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Null(result.ExchangeMic);
        Assert.Null(result.TradingCurrency);
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
                    Figi: "BBG001", ShareClassFigi: "BBGSC"),
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
    public async Task DiscoverListings_ByShareClass_ReturnsAllVenueTickersWithoutInventingIsin()
    {
        _openFigiClientMock.Setup(x => x.MapByShareClassFigiAsync("BBGSC", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new(null, "VWRA", "Vanguard ETF", "LN", Figi: "BBGL1", ShareClassFigi: "BBGSC"),
                new(null, "VWCE", "Vanguard ETF", "GY", Figi: "BBGL2", ShareClassFigi: "BBGSC"),
                new(null, "OTHER", "Different share class", "GY", Figi: "BBGL3", ShareClassFigi: "OTHER")]);
        _avClientMock.Setup(x => x.SearchTicker(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var results = await CreateService().DiscoverListingsAsync(null, "BBGSC", TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.Ticker == "VWRA" && x.ExchangeMic == "XLON");
        Assert.Contains(results, x => x.Ticker == "VWCE" && x.ExchangeMic == "XETR");
        Assert.All(results, x => Assert.Null(x.Isin));
        _openFigiClientMock.Verify(x => x.MapByIsinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}