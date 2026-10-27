using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using Moq;
using Moq.Protected;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Assets.Providers;

[Trait("Category", "Unit")]
public sealed class AlphaVantageSymbolResolverTests
{
    private static InstrumentDiscoveryResultDto Instrument(string? mic = "XLON", string? currency = "GBP") => new() { Ticker = "VOD", ExchangeMic = mic, TradingCurrency = currency, DisplayName = "Vodafone", ProviderSymbol = "VOD.LON" };

    [Fact]
    public async Task ResolveAsync_RequiresExactTickerVenueAndCurrency()
    {
        var handler = new Handler("""{"bestMatches":[{"1. symbol":"VOD.LON","2. name":"Vodafone","3. type":"Equity","4. region":"United Kingdom","8. currency":"GBP"},{"1. symbol":"VOD.FRK","2. name":"Vodafone Frankfurt","3. type":"Equity","4. region":"Germany","8. currency":"GBP"}]}""");
        var sut = Create(handler);
        var result = await sut.ResolveAsync(Instrument(), TestContext.Current.CancellationToken);
        Assert.Equal("candidate", result.Status);
        Assert.Equal("VOD.LON", result.Symbol);
        Assert.Equal("LON", result.ExchangeCode);
        Assert.Equal("not found", (await sut.ResolveAsync(Instrument("XETR", "GBP"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("not found", (await sut.ResolveAsync(Instrument("XLON", "EUR"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("not found", (await sut.ResolveAsync(new InstrumentDiscoveryResultDto { Ticker = "OTHER", ExchangeMic = "XLON", TradingCurrency = "GBP" }, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task ResolveAsync_MissingKeyOrHttpFailureIsUnavailable()
    {
        Assert.Equal("provider unavailable", (await Create(new Handler("{}"), apiKey: "").ResolveAsync(Instrument(), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("provider unavailable", (await Create(new Handler("", HttpStatusCode.TooManyRequests)).ResolveAsync(Instrument(), TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task ResolveAsync_ConsumesSearchBudget()
    {
        var handler = new Handler("{}");
        var sut = Create(handler);
        for (var i = 0; i < 25; i++) await sut.ResolveAsync(Instrument(), TestContext.Current.CancellationToken);
        Assert.Equal("provider unavailable", (await sut.ResolveAsync(Instrument(), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(25, handler.RequestCount);
    }

    [Fact]
    public async Task ResolveAsync_MapsXetraSuffixAndPreservesProviderSymbol()
    {
        var handler = new Handler("""{"bestMatches":[{"1. symbol":"VWCE.DEX","2. name":"Vanguard","3. type":"ETF","4. region":"Germany","8. currency":"EUR"}]}""");
        var result = await Create(handler).ResolveAsync(new InstrumentDiscoveryResultDto { Ticker = "VWCE", ExchangeMic = "XETR", TradingCurrency = "EUR" }, TestContext.Current.CancellationToken);
        Assert.Equal("candidate", result.Status);
        Assert.Equal("VWCE.DEX", result.Symbol);
        Assert.Equal("DEX", result.ExchangeCode);
    }

    [Fact]
    public async Task ValidateAsync_UsesResolvedAlphaVantageSymbol()
    {
        var priceClient = new Mock<IAlphaVantageClient>();
        priceClient.Setup(x => x.GetDailySeries("VOD.LON", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StockPrice { Isin = "GB00BH4HKS39", Currency = new Currency(1, "GBP", "£"), PricePerUnit = 12m }]);
        var sut = Create(new Handler("{}"), priceClient: priceClient.Object);
        Assert.True(await sut.ValidateAsync(new(MarketDataProvider.AlphaVantage, 100, "candidate", "VOD.LON", "LON", "GBP"), TestContext.Current.CancellationToken));
    }

    private static AlphaVantageSymbolResolver Create(Handler handler, string apiKey = "key", IAlphaVantageClient? priceClient = null) =>
        new(new HttpClient(handler), new ConfigService(new ExternalServiceConfiguration { ServiceName = "AlphaVantage", BaseUrl = "https://example.test/query", ApiKey = apiKey, IsEnabled = true }),
            new AlphaVantageRequestBudget(), priceClient ?? Mock.Of<IAlphaVantageClient>());

    private sealed class ConfigService(ExternalServiceConfiguration config) : IExternalServiceConfigService
    {
        public ValueTask<ExternalServiceConfiguration> GetServiceAsync(string serviceName, CancellationToken ct = default) => ValueTask.FromResult(config);
        public Task SaveServiceAsync(ExternalServiceConfiguration cfg, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Handler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}