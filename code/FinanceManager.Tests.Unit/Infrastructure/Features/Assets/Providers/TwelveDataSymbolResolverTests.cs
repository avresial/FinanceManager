using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Assets.Providers;

[Trait("Category", "Unit")]
public sealed class TwelveDataSymbolResolverTests
{
    private static InstrumentDiscoveryResultDto Instrument(string? mic = "XLON", string? currency = "GBP") => new() { Ticker = "VOD", ExchangeMic = mic, TradingCurrency = currency };

    [Fact]
    public async Task ResolveAsync_RequiresExactTickerMicAndCurrency()
    {
        var handler = new Handler("""{"data":[{"symbol":"VOD","instrument_name":"Vodafone","exchange":"LSE","mic_code":"XLON","currency":"GBP","instrument_type":"Common Stock"}],"status":"ok"}""");
        var sut = Create(handler);
        var result = await sut.ResolveAsync(Instrument(), TestContext.Current.CancellationToken);
        Assert.Equal("candidate", result.Status);
        Assert.Equal("VOD:LSE", result.Symbol);
        Assert.Equal("LSE", result.ExchangeCode);
        Assert.Equal("not found", (await sut.ResolveAsync(Instrument("XETR", "GBP"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("not found", (await sut.ResolveAsync(Instrument("XLON", "EUR"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("not found", (await sut.ResolveAsync(new InstrumentDiscoveryResultDto { Ticker = "OTHER", ExchangeMic = "XLON", TradingCurrency = "GBP" }, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task ResolveAsync_HandlesMissingKeyHttpErrorAndExhaustedBudget()
    {
        Assert.Equal("provider unavailable", (await Create(new Handler("{}"), apiKey: "").ResolveAsync(Instrument(), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("provider unavailable", (await Create(new Handler("{}", HttpStatusCode.ServiceUnavailable)).ResolveAsync(Instrument(), TestContext.Current.CancellationToken)).Status);
        var options = Options.Create(new TwelveDataOptions { DailyCreditLimit = 0 });
        var noBudget = Create(new Handler("{}"), options: options);
        Assert.Equal("provider unavailable", (await noBudget.ResolveAsync(Instrument(), TestContext.Current.CancellationToken)).Status);
        var oneCreditHandler = new Handler();
        var oneCredit = Create(oneCreditHandler, options: Options.Create(new TwelveDataOptions { DailyCreditLimit = 1 }));
        var candidate = await oneCredit.ResolveAsync(Instrument(), TestContext.Current.CancellationToken);
        Assert.Equal("candidate", candidate.Status);
        Assert.False(await oneCredit.ValidateAsync(candidate, TestContext.Current.CancellationToken));
        Assert.Single(oneCreditHandler.RequestUris);
    }

    [Fact]
    public async Task ValidateAsync_UsesTwelveDataSeriesForCandidate()
    {
        var handler = new Handler();
        var sut = Create(handler);
        var candidate = await sut.ResolveAsync(Instrument(), TestContext.Current.CancellationToken);
        Assert.True(await sut.ValidateAsync(candidate, TestContext.Current.CancellationToken));
        Assert.Contains(handler.RequestUris, uri => uri.AbsolutePath.EndsWith("/time_series", StringComparison.Ordinal)
            && uri.Query.Contains("symbol=VOD&exchange=LSE", StringComparison.Ordinal));
    }

    private static TwelveDataSymbolResolver Create(Handler handler, string apiKey = "key", IOptions<TwelveDataOptions>? options = null)
    {
        options ??= Options.Create(new TwelveDataOptions());
        var config = new ConfigService(new ExternalServiceConfiguration { ServiceName = "TwelveData", BaseUrl = "https://example.test", ApiKey = apiKey, IsEnabled = true });
        var budget = new TwelveDataCreditBudget(options);
        var httpClient = new HttpClient(handler);
        var client = new TwelveDataClient(httpClient, NullLogger<TwelveDataClient>.Instance, options, config, budget);
        return new(httpClient, config, options, budget, client);
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
            var content = request.RequestUri!.AbsolutePath.EndsWith("/symbol_search", StringComparison.Ordinal)
                ? """{"data":[{"symbol":"VOD","instrument_name":"Vodafone","exchange":"LSE","mic_code":"XLON","currency":"GBP","instrument_type":"Common Stock"}],"status":"ok"}"""
                : """{"values":[{"datetime":"2026-10-25","close":"1.25"}],"status":"ok"}""";
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent((string.IsNullOrEmpty(body) ? content : body).Replace("2026-10-25", DateTime.UtcNow.ToString("yyyy-MM-dd"))) });
        }
    }
}