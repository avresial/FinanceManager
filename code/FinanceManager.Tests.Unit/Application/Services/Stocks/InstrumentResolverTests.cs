using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.Identity.Repositories;
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
    private readonly Mock<IAssetRepository> _assetRepositoryMock = new();
    private readonly Mock<IAssetListingRepository> _assetListingRepositoryMock = new();
    private readonly Mock<IMarketDataSymbolRepository> _marketDataSymbolRepositoryMock = new();
    private readonly Mock<IQuoteFactorResolver> _quoteFactorResolverMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly ILogger<InstrumentResolver> _logger = LoggerFactory.Create(_ => { }).CreateLogger<InstrumentResolver>();

    private InstrumentResolver CreateResolver()
    {
        _quoteFactorResolverMock
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1m);  // Default: no conversion

        // New asset-model upserts echo back the entity with a generated id so the resolver can
        // expose AssetListingId.
        _assetRepositoryMock
            .Setup(x => x.Upsert(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset a, CancellationToken _) => { a.Id = 1; return a; });
        _assetListingRepositoryMock
            .Setup(x => x.Upsert(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetListing l, CancellationToken _) => { l.Id = 42; return l; });
        _marketDataSymbolRepositoryMock
            .Setup(x => x.Upsert(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketDataSymbol s, CancellationToken _) => { s.Id = 7; return s; });

        return new(
            _openFigiClientMock.Object,
            _avClientMock.Object,
            _assetRepositoryMock.Object,
            _assetListingRepositoryMock.Object,
            _marketDataSymbolRepositoryMock.Object,
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
                    Currency: "GBP",
                    ShareClassFigi: "BBG001S5W9D1")
            });


        var result = await resolver.ResolveAsync("CSPX", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        Assert.NotNull(result.Match);
        Assert.Equal("BBG001S5W9D1", result.Match.ShareClassFigi);
        Assert.Equal("IE00B5BMR087", result.Match.Isin);
        Assert.Equal("CSPX.LON", result.Match.AlphaVantageSymbol);

        // Auto-resolved pair is mirrored into the new asset model so future lookups have a listing.
        _assetListingRepositoryMock.Verify(
            x => x.Upsert(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>()),
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
                    Currency: "USD",
                    ShareClassFigi: "BBG001S5N8V8"),
                new(
                    Isin: "US0378331005",
                    Ticker: "AAPL",
                    Name: "Apple Inc",
                    ExchCode: "LN",
                    Currency: "GBP",
                    ShareClassFigi: "BBG001S5N8V8")
            });


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
                    Currency: "USD",
                    ShareClassFigi: "BBG001S5TD05")
            });


        var result = await resolver.ResolveAsync("MSFT.US", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        Assert.NotNull(result.Match);
        Assert.Equal("BBG001S5TD05", result.Match.ShareClassFigi);
        Assert.Equal("US5949181045", result.Match.Isin);
    }

    [Fact]
    public async Task ResolveAsync_SingleAvMatchButMultipleDistinctFigis_StaysAmbiguous()
    {
        // No exchange hint, one AV match, but OpenFIGI returns two DIFFERENT share classes
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
                new(Isin: null, Ticker: "ABC", Name: "Ambiguous Co A", ExchCode: "US", Currency: "USD", ShareClassFigi: "BBG000000AAA"),
                new(Isin: null, Ticker: "ABC", Name: "Ambiguous Co B", ExchCode: "UN", Currency: "USD", ShareClassFigi: "BBG000000BBB")
            });


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
                    Currency: "GBP",
                    ShareClassFigi: "BBG001S5W9D2")
            });

        // 1 GBX = 0.01 GBP (contract: multiply from-amount by factor to get to-amount)
        _quoteFactorResolverMock
            .Setup(x => x.ResolveAsync("GBX", "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.01m);


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
    public async Task ResolveAsync_AutoResolved_PersistsAssetModelAndExposesListingId()
    {
        var resolver = CreateResolver();
        // AlphaVantage quotes in GBX (pence); OpenFIGI's canonical currency is GBP. The persisted
        // listing/symbol must keep the GBX quote currency + 0.01 multiplier, not the canonical GBP.
        _avClientMock
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "CSPX.LON", Name = "iShares Core S&P 500 ETF", Type = "ETF", Region = "GB", Currency = "GBX", MatchScore = 1m }
            });
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("CSPX", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: "IE00B5BMR087", Ticker: "CSPX", Name: "iShares Core S&P 500 ETF", ExchCode: "LN", Currency: "GBP",
                    CompositeFigi: "BBG001S5W9C0", ShareClassFigi: "BBG001S5W9D1")
            });
        _quoteFactorResolverMock
            .Setup(x => x.ResolveAsync("GBX", "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.01m);

        var result = await resolver.ResolveAsync("CSPX", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        Assert.NotNull(result.Match);
        Assert.Equal(42, result.Match.AssetListingId);

        // Asset is upserted keyed on the share-class FIGI, with FIGI identifiers and the optional
        // ISIN cross-reference attached.
        _assetRepositoryMock.Verify(
            x => x.Upsert(
                It.Is<Asset>(a => a.ShareClassFigi == "BBG001S5W9D1"
                    && a.CompositeFigi == "BBG001S5W9C0"
                    && a.Isin == "IE00B5BMR087"
                    && a.Type == AssetType.ETF
                    && a.Identifiers.Any(i => i.Type == AssetIdentifierType.ShareClassFIGI && i.Value == "BBG001S5W9D1" && i.IsPrimary)
                    && a.Identifiers.Any(i => i.Type == AssetIdentifierType.ISIN && i.Value == "IE00B5BMR087")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // GBX listing carries the LSE MIC, the provider quote currency, the pence→pound multiplier,
        // and the base ticker — even though OpenFIGI's canonical currency was GBP.
        _assetListingRepositoryMock.Verify(
            x => x.Upsert(
                It.Is<AssetListing>(l => l.Ticker == "CSPX"
                    && l.ExchangeMic == "XLON"
                    && l.TradingCurrency == "GBX"
                    && l.PriceMultiplier == 0.01m
                    && l.IsPrimaryListing),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // The AlphaVantage symbol is mapped to the persisted listing in its quote currency (GBX).
        _marketDataSymbolRepositoryMock.Verify(
            x => x.Upsert(
                It.Is<MarketDataSymbol>(s => s.AssetListingId == 42
                    && s.Provider == MarketDataProvider.AlphaVantage
                    && s.Symbol == "CSPX.LON"
                    && s.Currency == "GBX"
                    && s.IsPrimary && s.IsEnabled),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_UsCompositeVenue_DoesNotClaimSpecificMic()
    {
        // OpenFIGI "US" is a composite covering all US venues, so it must not be persisted as XNAS.
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("MSFT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "MSFT", Name = "Microsoft Corp", Type = "Equity", Region = "US", Currency = "USD", MatchScore = 1m }
            });
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("MSFT", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: "US5949181045", Ticker: "MSFT", Name: "Microsoft Corp", ExchCode: "US", Currency: "USD", ShareClassFigi: "BBG001S5TD05")
            });

        var result = await resolver.ResolveAsync("MSFT", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        _assetListingRepositoryMock.Verify(
            x => x.Upsert(
                It.Is<AssetListing>(l => l.ExchangeMic != "XNAS" && l.TradingCurrency == "USD" && l.PriceMultiplier == 1m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_TickerWithoutIsin_AutoResolvesAndPersistsOnFigi()
    {
        // Realistic ticker path: OpenFIGI returns FIGIs but no ISIN. Identity is FIGI-centric, so the
        // pair still auto-resolves and persists — with no ISIN and no ISIN identifier.
        var resolver = CreateResolver();
        _avClientMock
            .Setup(x => x.SearchTicker("MSFT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TickerSearchMatch>
            {
                new() { Symbol = "MSFT", Name = "Microsoft Corp", Type = "Equity", Region = "US", Currency = "USD", MatchScore = 1m }
            });
        _openFigiClientMock
            .Setup(x => x.MapByTickerAsync("MSFT", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenFigiListing>
            {
                new(Isin: null, Ticker: "MSFT", Name: "Microsoft Corp", ExchCode: "US", Currency: "USD",
                    CompositeFigi: "BBG000BPH459", ShareClassFigi: "BBG001S5TD05")
            });

        var result = await resolver.ResolveAsync("MSFT", TestContext.Current.CancellationToken);

        Assert.Equal(ResolutionKind.AutoResolved, result.Kind);
        Assert.NotNull(result.Match);
        Assert.Equal("BBG001S5TD05", result.Match.ShareClassFigi);
        Assert.Null(result.Match.Isin);
        Assert.Equal("MSFT", result.Match.AlphaVantageSymbol);

        // Persisted with the FIGI identity and no ISIN / no ISIN identifier.
        _assetRepositoryMock.Verify(
            x => x.Upsert(
                It.Is<Asset>(a => a.ShareClassFigi == "BBG001S5TD05"
                    && a.Isin == null
                    && a.Identifiers.Any(i => i.Type == AssetIdentifierType.ShareClassFIGI && i.Value == "BBG001S5TD05")
                    && a.Identifiers.All(i => i.Type != AssetIdentifierType.ISIN)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}