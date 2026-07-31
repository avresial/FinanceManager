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

    private static StockPrice Price(decimal value) => new()
    {
        Isin = "US0378331005",
        PricePerUnit = value,
        Currency = _usd,
        Date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    };

    private sealed class FakeSource(
        string name,
        IReadOnlyList<StockPrice> result,
        MarketDataProvider? provider = null,
        int priority = int.MaxValue) : IStockPriceSource
    {
        public string Name => name;
        public MarketDataProvider? Provider => provider;
        public int Priority => priority;
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<StockPrice>> GetDailySeries(string symbol, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}