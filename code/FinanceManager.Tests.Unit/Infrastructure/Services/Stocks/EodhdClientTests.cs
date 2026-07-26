using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Services.Stocks;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class EodhdClientTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly DateTime _start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc);

    private static EodhdClient CreateClient(MockHttpMessageHandler handler, string apiKey = "test-key")
    {
        var httpClient = new HttpClient(handler);
        var logger = LoggerFactory.Create(b => { }).CreateLogger<EodhdClient>();
        var config = new ExternalServiceConfiguration
        {
            ServiceName = "Eodhd",
            BaseUrl = "https://eodhd.com/api",
            ApiKey = apiKey,
            IsEnabled = true,
        };
        return new EodhdClient(httpClient, logger, new StubExternalServiceConfigService(config));
    }

    [Fact]
    public async Task GetDailySeries_PrefersAdjustedClose()
    {
        var handler = new MockHttpMessageHandler(response: """
            [
              { "date": "2024-01-02", "close": 100.0, "adjusted_close": 95.5 },
              { "date": "2024-01-03", "close": 110.0, "adjusted_close": 105.5 }
            ]
            """);
        var client = CreateClient(handler);

        var result = await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(95.5m, result[0].PricePerUnit);
        Assert.Equal("US0378331005", result[0].Isin);
        Assert.Equal(_usd, result[0].Currency);
    }

    [Fact]
    public async Task GetDailySeries_FallsBackToRawClose_WhenAdjustedMissing()
    {
        var handler = new MockHttpMessageHandler(response: """
            [ { "date": "2024-01-02", "close": 100.0 } ]
            """);
        var client = CreateClient(handler);

        var result = await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(100.0m, result[0].PricePerUnit);
    }

    [Fact]
    public async Task GetDailySeries_DefaultsBareSymbolToUsExchange()
    {
        var handler = new MockHttpMessageHandler(response: "[]");
        var client = CreateClient(handler);

        await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("/eod/AAPL.US", handler.LastRequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetDailySeries_PassesSuffixedSymbolThrough()
    {
        var handler = new MockHttpMessageHandler(response: "[]");
        var client = CreateClient(handler);

        await client.GetDailySeries("CSPX.LON", "IE00B5BMR087", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("/eod/CSPX.LON", handler.LastRequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetDailySeries_FiltersOutOfRangeDates()
    {
        var handler = new MockHttpMessageHandler(response: """
            [
              { "date": "2023-12-31", "adjusted_close": 90.0 },
              { "date": "2024-01-15", "adjusted_close": 95.0 },
              { "date": "2024-02-15", "adjusted_close": 99.0 }
            ]
            """);
        var client = CreateClient(handler);

        var result = await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(95.0m, result[0].PricePerUnit);
    }

    [Fact]
    public async Task GetDailySeries_WithMissingApiKey_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler(response: "[]");
        var client = CreateClient(handler, apiKey: "");

        var result = await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Null(handler.LastRequestUri); // never hit the network
    }

    [Fact]
    public async Task GetDailySeries_WhenApiFails_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler(statusCode: HttpStatusCode.TooManyRequests, response: "rate limited");
        var client = CreateClient(handler);

        var result = await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    private sealed class StubExternalServiceConfigService(ExternalServiceConfiguration config) : IExternalServiceConfigService
    {
        public ValueTask<ExternalServiceConfiguration> GetServiceAsync(string serviceName, CancellationToken ct = default) =>
            ValueTask.FromResult(config);

        public ValueTask<IReadOnlyList<ExternalServiceConfiguration>> GetAllServicesAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<ExternalServiceConfiguration>>([config]);

        public Task SaveServiceAsync(ExternalServiceConfiguration cfg, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class MockHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string response = "") : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(response)
            });
        }
    }
}