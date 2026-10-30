using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Assets.Providers;

[Trait("Category", "Unit")]
public sealed class EodhdSymbolResolverTests
{
    private static InstrumentDiscoveryResultDto Instrument(string? mic = "XLON", string? currency = "GBP") => new() { Ticker = "VOD", ExchangeMic = mic, TradingCurrency = currency, Isin = "GB00BH4HKS39" };

    [Fact]
    public async Task ResolveAsync_RequiresExactTickerVenueAndCurrency()
    {
        var sut = Create(new Handler("""[{"Code":"VOD","Exchange":"LSE","Currency":"GBP","Name":"Vodafone","Type":"Common Stock","ISIN":"GB00BH4HKS39"}]"""));
        var result = await sut.ResolveAsync(Instrument(), TestContext.Current.CancellationToken);
        Assert.Equal("candidate", result.Status);
        Assert.Equal("VOD.LSE", result.Symbol);
        Assert.Equal("LSE", result.ExchangeCode);
        Assert.Equal("not found", (await sut.ResolveAsync(Instrument("XETR", "GBP"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("not found", (await sut.ResolveAsync(Instrument("XLON", "EUR"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("not found", (await sut.ResolveAsync(new InstrumentDiscoveryResultDto { Ticker = "OTHER", ExchangeMic = "XLON", TradingCurrency = "GBP" }, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task ResolveAsync_WhenMicOrCurrencyIsMissing_ReturnsNotFoundWithoutHttpCall()
    {
        var handler = new Handler();
        var sut = Create(handler);

        Assert.Equal("not found", (await sut.ResolveAsync(Instrument(mic: null, currency: "GBP"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("not found", (await sut.ResolveAsync(Instrument(mic: "XLON", currency: null), TestContext.Current.CancellationToken)).Status);
        Assert.Empty(handler.RequestUris);
    }

    [Fact]
    public async Task ResolveAsync_RejectsDifferentIsinEvenWhenTickerVenueAndCurrencyMatch()
    {
        var sut = Create(new Handler("""[{"Code":"VOD","Exchange":"LSE","Currency":"GBP","Name":"Other Vodafone line","ISIN":"GB00BH4HKS40"}]"""));
        Assert.Equal("not found", (await sut.ResolveAsync(Instrument(), TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task ValidateAsync_UsesEodhdSeriesForCandidate()
    {
        var handler = new Handler();
        var sut = Create(handler);
        var candidate = await sut.ResolveAsync(Instrument(), TestContext.Current.CancellationToken);
        Assert.True(await sut.ValidateAsync(candidate, TestContext.Current.CancellationToken));
        Assert.Contains(handler.RequestUris, uri => uri.AbsolutePath.EndsWith("/eod/VOD.LSE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_MissingKeyAndHttpFailureAreUnavailable()
    {
        Assert.Equal("provider unavailable", (await Create(new Handler("[]"), apiKey: "").ResolveAsync(Instrument(), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("provider unavailable", (await Create(new Handler("", HttpStatusCode.TooManyRequests)).ResolveAsync(Instrument(), TestContext.Current.CancellationToken)).Status);
    }

    private static EodhdSymbolResolver Create(Handler handler, string apiKey = "key")
    {
        var config = new ConfigService(new ExternalServiceConfiguration { ServiceName = "Eodhd", BaseUrl = "https://example.test/api", ApiKey = apiKey, IsEnabled = true });
        var httpClient = new HttpClient(handler);
        return new(httpClient, config, new EodhdClient(httpClient, NullLogger<EodhdClient>.Instance, config));
    }
    private sealed class ConfigService(ExternalServiceConfiguration config) : IExternalServiceConfigService
    {
        public ValueTask<ExternalServiceConfiguration> GetServiceAsync(string serviceName, CancellationToken ct = default) => ValueTask.FromResult(config);
        public Task SaveServiceAsync(ExternalServiceConfiguration cfg, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class Handler(string body = "", HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUris.Add(request.RequestUri!);
            var content = request.RequestUri!.AbsolutePath.Contains("/search/", StringComparison.Ordinal)
                ? """[{"Code":"VOD","Exchange":"LSE","Currency":"GBP","Name":"Vodafone","Type":"Common Stock","ISIN":"GB00BH4HKS39"}]"""
                : """[{"date":"2026-10-25","adjusted_close":1.25}]""";
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent((string.IsNullOrEmpty(body) ? content : body).Replace("2026-10-25", DateTime.UtcNow.ToString("yyyy-MM-dd"))) });
        }
    }
}