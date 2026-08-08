using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Assets.Providers;

[Trait("Category", "Unit")]
public class TwelveDataClientTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly DateTime _start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetDailySeries_MapsDailyPricesAndExchange()
    {
        var handler = new MockHttpMessageHandler("""
            {
              "meta": { "type": "ETF" },
              "values": [
                { "datetime": "2024-01-02", "close": "95.50" },
                { "datetime": "2024-02-01", "close": "99.00" }
              ],
              "status": "ok"
            }
            """);
        var client = CreateClient(handler);

        var result = await client.GetDailySeries(
            "CSPX:LSE", "IE00B5BMR087", _start, _end, _usd, TestContext.Current.CancellationToken);

        var price = Assert.Single(result);
        Assert.Equal(95.50m, price.PricePerUnit);
        Assert.Equal("IE00B5BMR087", price.Isin);
        Assert.Contains("symbol=CSPX", handler.LastRequestUri!.Query);
        Assert.Contains("exchange=LSE", handler.LastRequestUri.Query);
        Assert.Contains("interval=1day", handler.LastRequestUri.Query);
        Assert.Equal("apikey test-key", handler.LastAuthorization);
        Assert.DoesNotContain("test-key", handler.LastRequestUri.Query);
        Assert.Contains(AssetType.ETF, client.SupportedAssetTypes);
        Assert.Contains("1day", client.SupportedIntervals);
    }

    [Fact]
    public async Task GetLatestQuote_MapsQuote()
    {
        var client = CreateClient(new MockHttpMessageHandler(
            """{ "symbol": "AAPL", "datetime": "2024-01-15", "close": "188.25" }"""));

        var result = await client.GetLatestQuote(
            "AAPL", "US0378331005", _usd, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(188.25m, result.PricePerUnit);
        Assert.Equal(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), result.Date);
    }

    [Fact]
    public async Task GetLatestQuote_ReturnsNullForInvalidResponseShape()
    {
        var client = CreateClient(new MockHttpMessageHandler("""{ "close": {} }"""));

        var result = await client.GetLatestQuote(
            "AAPL", "US0378331005", _usd, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDailySeries_ReturnsEmptyForInvalidResponseShape()
    {
        var client = CreateClient(new MockHttpMessageHandler("""{ "values": {} }"""));

        var result = await client.GetDailySeries(
            "AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDailyRatesAsync_MapsForexPair()
    {
        var handler = new MockHttpMessageHandler("""
            {
              "values": [{ "datetime": "2024-01-15", "close": "1.095" }],
              "status": "ok"
            }
            """);
        var client = CreateClient(handler);

        var result = await client.GetDailyRatesAsync("EUR", "USD", TestContext.Current.CancellationToken);

        Assert.Equal(1.095m, Assert.Single(result.Points).Value);
        Assert.Contains("symbol=EUR%2FUSD", handler.LastRequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetDailyRatesAsync_ReturnsRateLimitedFor429()
    {
        var client = CreateClient(new MockHttpMessageHandler("{}", HttpStatusCode.TooManyRequests));

        var result = await client.GetDailyRatesAsync("EUR", "USD", TestContext.Current.CancellationToken);

        Assert.Equal(FinanceManager.Application.Backfill.Currencies.FxDailyStatus.RateLimited, result.Status);
    }

    [Fact]
    public async Task GetDailyRatesAsync_ReturnsRateLimitedForApiErrorBody()
    {
        var client = CreateClient(new MockHttpMessageHandler(
            """{ "code": 429, "message": "API credits limit reached", "status": "error" }"""));

        var result = await client.GetDailyRatesAsync("EUR", "USD", TestContext.Current.CancellationToken);

        Assert.Equal(FinanceManager.Application.Backfill.Currencies.FxDailyStatus.RateLimited, result.Status);
    }

    [Fact]
    public async Task GetDailyRatesAsync_WhenHttpTimesOut_ReturnsFailed()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("request timeout"));
        var logger = new Mock<ILogger>();
        var client = CreateClient(handler.Object, CreateLogger(logger));

        var result = await client.GetDailyRatesAsync("EUR", "USD", TestContext.Current.CancellationToken);

        Assert.Equal(FinanceManager.Application.Backfill.Currencies.FxDailyStatus.Error, result.Status);
        Assert.Empty(result.Points);
        Assert.Contains(LogLevel.Debug, GetLogLevels(logger));
    }

    [Fact]
    public async Task GetDailyRatesAsync_WhenCallerCancels_RethrowsAndLogsDebug()
    {
        using var cancellation = new CancellationTokenSource();
        var logger = new Mock<ILogger>();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((_, _) => cancellation.Cancel())
            .ThrowsAsync(new OperationCanceledException("caller cancelled", cancellation.Token));
        var client = CreateClient(handler.Object, CreateLogger(logger));

#pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetDailyRatesAsync("EUR", "USD", cancellation.Token));
#pragma warning restore xUnit1051

        var levels = GetLogLevels(logger);
        Assert.Contains(LogLevel.Debug, levels);
        Assert.DoesNotContain(LogLevel.Error, levels);
    }

    private static TwelveDataClient CreateClient(
        HttpMessageHandler handler,
        ILogger<TwelveDataClient>? logger = null)
    {
        var options = Options.Create(new TwelveDataOptions());
        var config = new ExternalServiceConfiguration
        {
            ServiceName = "TwelveData",
            BaseUrl = "https://api.twelvedata.com",
            ApiKey = "test-key",
            IsEnabled = true
        };
        return new TwelveDataClient(
            new HttpClient(handler),
            logger ?? NullLogger<TwelveDataClient>.Instance,
            options,
            new StubConfigService(config),
            new TwelveDataCreditBudget(options));
    }

    private static ILogger<TwelveDataClient> CreateLogger(Mock<ILogger> logger)
    {
        var factory = new Mock<ILoggerFactory>();
        factory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        return factory.Object.CreateLogger<TwelveDataClient>();
    }

    private static List<LogLevel> GetLogLevels(Mock<ILogger> logger) =>
        logger.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
            .Select(invocation => (LogLevel)invocation.Arguments[0])
            .ToList();

    private sealed class StubConfigService(ExternalServiceConfiguration config) : IExternalServiceConfigService
    {
        public ValueTask<ExternalServiceConfiguration> GetServiceAsync(string serviceName, CancellationToken ct = default) =>
            ValueTask.FromResult(config);

        public ValueTask<IReadOnlyList<ExternalServiceConfiguration>> GetAllServicesAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<ExternalServiceConfiguration>>([config]);

        public Task SaveServiceAsync(ExternalServiceConfiguration config, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class MockHttpMessageHandler(
        string response,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(response) });
        }
    }
}