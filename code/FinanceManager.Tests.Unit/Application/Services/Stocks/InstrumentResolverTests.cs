using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Stocks;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InstrumentResolverTests
{
    private readonly Mock<IOpenFigiClient> _openFigiClientMock = new();
    private readonly Mock<IAlphaVantageClient> _avClientMock = new();
    private readonly Mock<IStockDetailsRepository> _stockDetailsRepositoryMock = new();
    private readonly Mock<ICurrencyRepository> _currencyRepositoryMock = new();
    private readonly Mock<IQuoteFactorResolver> _quoteFactorResolverMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly ILogger<InstrumentResolver> _logger = LoggerFactory.Create(_ => { }).CreateLogger<InstrumentResolver>();

    private InstrumentResolver CreateResolver()
    {
        _quoteFactorResolverMock
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1m);  // Default: no conversion

        _currencyRepositoryMock
            .Setup(x => x.GetOrAdd(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string shortName, string? symbol, CancellationToken _) =>
                new Currency { ShortName = shortName, Symbol = symbol ?? shortName });

        _stockDetailsRepositoryMock
            .Setup(x => x.Add(It.IsAny<StockDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails d, CancellationToken _) => d);

        return new(
            _openFigiClientMock.Object,
            _avClientMock.Object,
            _stockDetailsRepositoryMock.Object,
            _currencyRepositoryMock.Object,
            _quoteFactorResolverMock.Object,
            _cache,
            _logger);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_WithNullOrWhitespaceTicker_ReturnsNoMatch(string? input)
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(input!, TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.NoMatch, result.Kind);
    }

    [Fact]
    public async Task ResolveAsync_WithSingleUnambiguousMatch_ReturnsAutoResolved()
    {
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new()
                {
                    Symbol = "CSPX.LON",
                    Name = "iShares Core S&P 500 ETF",
                    Type = "ETF",
                    Region = "GB",
                    Currency = "GBP",
                    MatchScore = 1m
                }
            });

        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(
                    Isin: "IE00B5BMR087",
                    Ticker: "CSPX",
                    Name: "iShares Core S&P 500 ETF",
                    ExchCode: "LN",
                    Currency: "GBP")
            });

        _stockDetailsRepositoryMock
            .Setup(x => x.GetByTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails?)null);

        var result = await resolver.ResolveAsync("CSPX", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        Assert.NotNull(result.Match);
        Assert.Equal("IE00B5BMR087", result.Match.Isin);
        Assert.Equal("CSPX.LON", result.Match.AlphaVantageSymbol);

        // Auto-resolved pair must be persisted so future lookups skip the providers.
        _stockDetailsRepositoryMock.Verify(
            x => x.Add(
                It.Is<StockDetails>(d => d.Isin == "IE00B5BMR087" && d.AlphaVantageSymbol == "CSPX.LON"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WithMultipleExchanges_ReturnsCandidatesAmbiguous()
    {
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new()
                {
                    Symbol = "AAPL",
                    Name = "Apple Inc",
                    Type = "Equity",
                    Region = "US",
                    Currency = "USD",
                    MatchScore = 1m
                },
                new()
                {
                    Symbol = "AAPL.LSE",
                    Name = "Apple Inc",
                    Type = "Equity",
                    Region = "GB",
                    Currency = "GBP",
                    MatchScore = 0.8m
                }
            });

        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("AAPL", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(
                    Isin: "US0378331005",
                    Ticker: "AAPL",
                    Name: "Apple Inc",
                    ExchCode: "US",
                    Currency: "USD"),
                new(
                    Isin: "US0378331005",
                    Ticker: "AAPL",
                    Name: "Apple Inc",
                    ExchCode: "LN",
                    Currency: "GBP")
            });

        _stockDetailsRepositoryMock
            .Setup(x => x.GetByTicker("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails?)null);

        var result = await resolver.ResolveAsync("AAPL", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.Ambiguous, result.Kind);
        Assert.True(result.Candidates.Count >= 2);
    }

    [Fact]
    public async Task ResolveAsync_WithExchangeHintDisambiguates_ReturnsAutoResolved()
    {
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("MSFT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new()
                {
                    Symbol = "MSFT",
                    Name = "Microsoft Corp",
                    Type = "Equity",
                    Region = "US",
                    Currency = "USD",
                    MatchScore = 1m
                }
            });

        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("MSFT", "US", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(
                    Isin: "US5949181045",
                    Ticker: "MSFT",
                    Name: "Microsoft Corp",
                    ExchCode: "US",
                    Currency: "USD")
            });

        _stockDetailsRepositoryMock
            .Setup(x => x.GetByTicker("MSFT", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails?)null);

        var result = await resolver.ResolveAsync("MSFT.US", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        Assert.NotNull(result.Match);
        Assert.Equal("US5949181045", result.Match.Isin);
    }

    [Fact]
    public async Task ResolveAsync_SingleAvMatchButMultipleDistinctIsins_StaysAmbiguous()
    {
        // No exchange hint, one AV match, but OpenFIGI returns two DIFFERENT instruments
        // sharing the ticker — the resolver must not auto-resolve an arbitrary one.
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("ABC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "ABC", Name = "Ambiguous Co", Type = "Equity", Region = "US", Currency = "USD", MatchScore = 1m }
            });

        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("ABC", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: "US1111111111", Ticker: "ABC", Name: "Ambiguous Co A", ExchCode: "US", Currency: "USD"),
                new(Isin: "US2222222222", Ticker: "ABC", Name: "Ambiguous Co B", ExchCode: "UN", Currency: "USD")
            });

        _stockDetailsRepositoryMock
            .Setup(x => x.GetByTicker("ABC", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails?)null);

        var result = await resolver.ResolveAsync("ABC", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.Ambiguous, result.Kind);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public async Task ResolveAsync_OpenFigiDown_KeepsAlphaVantageCurrency()
    {
        // During an OpenFIGI outage the AV-only candidate must retain the AV currency,
        // not be forced to USD.
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "CSPX.LON", Name = "iShares Core S&P 500 ETF", Type = "ETF", Region = "GB", Currency = "GBP", MatchScore = 1m }
            });

        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("OpenFIGI unavailable"));

        _stockDetailsRepositoryMock
            .Setup(x => x.GetByTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails?)null);

        var result = await resolver.ResolveAsync("CSPX", TestContext.Current.CancellationToken);

        var candidate = result.Match ?? Assert.Single(result.Candidates);
        Assert.Equal("GBP", candidate.Currency);
        Assert.Equal(1m, candidate.QuoteFactor);
        Assert.Null(candidate.Isin);
    }

    [Fact]
    public async Task ResolveAsync_WithNoMatch_ReturnsNoMatch()
    {
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("INVALIDTICKER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>());

        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("INVALIDTICKER", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>());

        _stockDetailsRepositoryMock
            .Setup(x => x.GetByTicker("INVALIDTICKER", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails?)null);

        var result = await resolver.ResolveAsync("INVALIDTICKER", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.NoMatch, result.Kind);
    }

    [Fact]
    public async Task ResolveAsync_WithOpenFigiFailure_FallsBackToAvOnly()
    {
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new()
                {
                    Symbol = "CSPX.LON",
                    Name = "iShares Core S&P 500 ETF",
                    Type = "ETF",
                    Region = "GB",
                    Currency = "GBP",
                    MatchScore = 1m
                }
            });

        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("OpenFIGI API unavailable"));

        _stockDetailsRepositoryMock
            .Setup(x => x.GetByTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails?)null);

        var result = await resolver.ResolveAsync("CSPX", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.Kind is ResolutionKind.Ambiguous or ResolutionKind.AutoResolved);
    }

    [Fact]
    public async Task ResolveAsync_WithGbxCurrency_HandlesQuoteFactor()
    {
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("VANGUARD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new()
                {
                    Symbol = "VANGUARD.LSE",
                    Name = "Vanguard FTSE All-Share ETF",
                    Type = "ETF",
                    Region = "GB",
                    Currency = "GBX",
                    MatchScore = 1m
                }
            });

        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("VANGUARD", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(
                    Isin: "IE00B4L5Y983",
                    Ticker: "VANGUARD",
                    Name: "Vanguard FTSE All-Share ETF",
                    ExchCode: "LN",
                    Currency: "GBP")
            });

        // 1 GBX = 0.01 GBP (contract: multiply from-amount by factor to get to-amount)
        _quoteFactorResolverMock
            .Setup(x => x.ResolveAsync("GBX", "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.01m);

        _stockDetailsRepositoryMock
            .Setup(x => x.GetByTicker("VANGUARD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails?)null);

        var result = await resolver.ResolveAsync("VANGUARD", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        Assert.NotNull(result.Match);
        Assert.Equal("GBP", result.Match.Currency);
        Assert.Equal(0.01m, result.Match.QuoteFactor);
        _quoteFactorResolverMock.Verify(
            x => x.ResolveAsync("GBX", "GBP", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WithCachedStockDetails_ReturnsCachedMatch()
    {
        var resolver = CreateResolver();
        var gbp = new Currency { ShortName = "GBP", Symbol = "£" };
        var existing = new StockDetails
        {
            Isin = "IE00B5BMR087",
            Ticker = "CSPX",
            AlphaVantageSymbol = "CSPX.LON",
            Name = "iShares Core S&P 500 ETF",
            Type = "ETF",
            Region = "GB",
            Currency = gbp
        };

        _stockDetailsRepositoryMock
            .Setup(x => x.GetByTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await resolver.ResolveAsync("CSPX", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        Assert.NotNull(result.Match);
        Assert.Equal("IE00B5BMR087", result.Match.Isin);
        Assert.Equal("CSPX.LON", result.Match.AlphaVantageSymbol);

        _avClientMock.Verify(x => x.SearchTicker(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _openFigiClientMock.Verify(x => x.MapByTickerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}