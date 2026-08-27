using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Tests.Unit.Application.Services.Stocks;

[Trait("Category", "Unit")]
public class FallbackStockPriceSourceTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly DateTime _start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc);

    private static FallbackStockPriceSource Create(params IStockPriceSource[] sources) =>
        new(sources, LoggerFactory.Create(b => { }).CreateLogger<FallbackStockPriceSource>());

    [Fact]
    public async Task ReturnsPrimaryResult_WithoutCallingFallback()
    {
        var primary = new FakeSource("Primary", [Price(100m)]);
        var fallback = new FakeSource("Fallback", [Price(200m)]);
        var sut = Create(primary, fallback);

        var result = await sut.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(100m, result[0].PricePerUnit);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, fallback.CallCount);
    }

    [Fact]
    public async Task FallsThroughToSecondary_WhenPrimaryEmpty()
    {
        var primary = new FakeSource("Primary", []);
        var fallback = new FakeSource("Fallback", [Price(200m)]);
        var sut = Create(primary, fallback);

        var result = await sut.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(200m, result[0].PricePerUnit);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, fallback.CallCount);
    }

    [Fact]
    public async Task ReturnsEmpty_WhenAllSourcesEmpty()
    {
        var primary = new FakeSource("Primary", []);
        var fallback = new FakeSource("Fallback", []);
        var sut = Create(primary, fallback);

        var result = await sut.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, fallback.CallCount);
    }

    [Fact]
    public async Task UsesPriorityAndOnlyCallsProviderMatchingTheSymbol()
    {
        var twelveData = new FakeSource(
            "TwelveData", [Price(200m)], MarketDataProvider.TwelveData, priority: 50);
        var alphaVantage = new FakeSource(
            "AlphaVantage", [Price(100m)], MarketDataProvider.AlphaVantage, priority: 100);
        var sut = Create(alphaVantage, twelveData);

        var prioritized = await sut.GetDailySeries(
            "AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);
        var providerSpecific = await sut.GetDailySeries(
            MarketDataProvider.AlphaVantage,
            "AAPL",
            "US0378331005",
            _start,
            _end,
            _usd,
            TestContext.Current.CancellationToken);

        Assert.Equal(200m, Assert.Single(prioritized).PricePerUnit);
        Assert.Equal(100m, Assert.Single(providerSpecific).PricePerUnit);
        Assert.Equal(1, alphaVantage.CallCount);
        Assert.Equal(1, twelveData.CallCount);
    }

    [Fact]
    public async Task FallsThroughToSecondary_WhenPrimaryThrows()
    {
        var primary = new FakeSource("Primary", throwException: true);
        var fallback = new FakeSource("Fallback", [Price(200m)]);
        var sut = Create(primary, fallback);

        var result = await sut.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Equal(200m, Assert.Single(result).PricePerUnit);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, fallback.CallCount);
    }

    [Fact]
    public async Task GetDailySeries_CallerCancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var primary = new FakeSource("Primary", [Price(100m)]);
        var fallback = new FakeSource("Fallback", [Price(200m)]);
        var sut = Create(primary, fallback);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, cts.Token));

        Assert.Equal(0, primary.CallCount);
        Assert.Equal(0, fallback.CallCount);
    }

    [Fact]
    public async Task GetLatestQuote_ReturnsPrimaryResult_WithoutCallingFallback()
    {
        var primary = new FakeSource("Primary", [Price(100m)], latestQuote: Price(100m));
        var fallback = new FakeSource("Fallback", [Price(200m)], latestQuote: Price(200m));
        var sut = Create(primary, fallback);

        var result = await sut.GetLatestQuote("AAPL", "US0378331005", _usd, TestContext.Current.CancellationToken);

        Assert.Equal(100m, Assert.IsType<StockPrice>(result).PricePerUnit);
        Assert.Equal(1, primary.QuoteCallCount);
        Assert.Equal(0, fallback.QuoteCallCount);
    }

    [Fact]
    public async Task GetLatestQuote_FallsThroughToSecondary_WhenPrimaryThrows()
    {
        var primary = new FakeSource("Primary", throwException: true);
        var fallback = new FakeSource("Fallback", [Price(200m)], latestQuote: Price(200m));
        var sut = Create(primary, fallback);

        var result = await sut.GetLatestQuote("AAPL", "US0378331005", _usd, TestContext.Current.CancellationToken);

        Assert.Equal(200m, Assert.IsType<StockPrice>(result).PricePerUnit);
        Assert.Equal(1, primary.QuoteCallCount);
        Assert.Equal(1, fallback.QuoteCallCount);
    }

    [Fact]
    public async Task GetLatestQuote_CallerCancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var primary = new FakeSource("Primary", [Price(100m)], latestQuote: Price(100m));
        var fallback = new FakeSource("Fallback", [Price(200m)], latestQuote: Price(200m));
        var sut = Create(primary, fallback);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.GetLatestQuote("AAPL", "US0378331005", _usd, cts.Token));

        Assert.Equal(0, primary.QuoteCallCount);
        Assert.Equal(0, fallback.QuoteCallCount);
    }

    [Fact]
    public async Task GetDailySeries_WithProvider_WhenThrows_ReturnsEmpty()
    {
        var alphaVantage = new FakeSource(
            "AlphaVantage",
            [],
            MarketDataProvider.AlphaVantage,
            priority: 100,
            throwException: true);
        var sut = Create(alphaVantage);

        var result = await sut.GetDailySeries(
            MarketDataProvider.AlphaVantage,
            "AAPL",
            "US0378331005",
            _start,
            _end,
            _usd,
            TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Equal(1, alphaVantage.CallCount);
    }

    [Fact]
    public async Task GetDailySeries_WithProvider_CallerCancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var alphaVantage = new FakeSource(
            "AlphaVantage",
            [Price(100m)],
            MarketDataProvider.AlphaVantage,
            priority: 100);
        var sut = Create(alphaVantage);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.GetDailySeries(
                MarketDataProvider.AlphaVantage,
                "AAPL",
                "US0378331005",
                _start,
                _end,
                _usd,
                cts.Token));

        Assert.Equal(0, alphaVantage.CallCount);
    }

    private static StockPrice Price(decimal value) => new()
    {
        Isin = "US0378331005",
        PricePerUnit = value,
        Currency = _usd,
        Date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    };

    private sealed class FakeSource(
        string name,
        IReadOnlyList<StockPrice>? result = null,
        MarketDataProvider? provider = null,
        int priority = int.MaxValue,
        bool throwException = false,
        StockPrice? latestQuote = null) : IStockPriceSource
    {
        public string Name => name;
        public MarketDataProvider? Provider => provider;
        public int Priority => priority;
        public int CallCount { get; private set; }
        public int QuoteCallCount { get; private set; }

        public Task<IReadOnlyList<StockPrice>> GetDailySeries(string symbol, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            if (throwException)
                throw new HttpRequestException("Simulated provider outage");
            return Task.FromResult(result ?? []);
        }

        public Task<StockPrice?> GetLatestQuote(string symbol, string isin, Currency currency, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            QuoteCallCount++;
            if (throwException)
                throw new HttpRequestException("Simulated provider quote outage");
            return Task.FromResult(latestQuote);
        }
    }
}