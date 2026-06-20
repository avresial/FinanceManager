using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Infrastructure.Services.Stocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Services.Stocks;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class AlphaVantageClientTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly DateTime _start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc);

    private static AlphaVantageClient CreateClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var logger = LoggerFactory.Create(b => { }).CreateLogger<AlphaVantageClient>();
        var options = Options.Create(new StockApiOptions { OutputSize = "compact" });
        var config = new ExternalServiceConfiguration
        {
            ServiceName = "AlphaVantage",
            BaseUrl = "https://www.alphavantage.co/query",
            ApiKey = "test-key",
            IsEnabled = true,
        };
        return new AlphaVantageClient(httpClient, logger, options, new StubExternalServiceConfigService(config));
    }

    [Fact]
    public async Task GetDailySeries_UsesAdjustedEndpoint()
    {
        var handler = new MockHttpMessageHandler(response: _emptySeries);
        var client = CreateClient(handler);

        await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("function=TIME_SERIES_DAILY_ADJUSTED", handler.LastRequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetDailySeries_PrefersAdjustedClose()
    {
        var handler = new MockHttpMessageHandler(response: """
            {
              "Time Series (Daily)": {
                "2024-01-15": { "4. close": "190.0000", "5. adjusted close": "188.5000" }
              }
            }
            """);
        var client = CreateClient(handler);

        var result = await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(188.5m, result[0].PricePerUnit);
        Assert.Equal("US0378331005", result[0].Isin);
    }

    [Fact]
    public async Task GetDailySeries_FallsBackToRawClose_WhenAdjustedAbsent()
    {
        var handler = new MockHttpMessageHandler(response: """
            {
              "Time Series (Daily)": {
                "2024-01-15": { "4. close": "190.0000" }
              }
            }
            """);
        var client = CreateClient(handler);

        var result = await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(190.0m, result[0].PricePerUnit);
    }

    [Fact]
    public async Task GetDailySeries_WhenSeriesMissing_ReturnsEmpty()
    {
        // Premium-gated / rate-limited responses omit the series entirely; we must return empty
        // so the fallback price source can take over rather than throwing.
        var handler = new MockHttpMessageHandler(response: """
            { "Information": "premium endpoint" }
            """);
        var client = CreateClient(handler);

        var result = await client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    private const string _emptySeries = """{ "Time Series (Daily)": {} }""";

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