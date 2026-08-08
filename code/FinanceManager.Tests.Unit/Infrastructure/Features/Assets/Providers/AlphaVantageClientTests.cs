using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Assets.Providers;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class AlphaVantageClientTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly DateTime _start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc);

    private static AlphaVantageClient CreateClient(
        HttpMessageHandler handler,
        ILogger<AlphaVantageClient>? logger = null)
    {
        var httpClient = new HttpClient(handler);
        logger ??= LoggerFactory.Create(b => { }).CreateLogger<AlphaVantageClient>();
        var config = new ExternalServiceConfiguration
        {
            ServiceName = "AlphaVantage",
            BaseUrl = "https://www.alphavantage.co/query",
            ApiKey = "test-key",
            IsEnabled = true,
        };
        return new AlphaVantageClient(httpClient, logger, new StubExternalServiceConfigService(config));
    }

    [Fact]
    public async Task GetDailySeries_UsesFreeDailyEndpoint()
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

        Assert.Single(handler.RequestUris);
        Assert.DoesNotContain("TIME_SERIES_DAILY_ADJUSTED", handler.RequestUris[0].AbsoluteUri);
        Assert.Contains("function=TIME_SERIES_DAILY&", handler.RequestUris[0].AbsoluteUri);
        Assert.Contains("outputsize=compact", handler.RequestUris[0].AbsoluteUri);
        Assert.Equal(190m, Assert.Single(result).PricePerUnit);
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

    [Fact]
    public async Task GetDailySeries_WhenCallerCancels_RethrowsAndLogsDebug()
    {
        var logger = new ListLogger<AlphaVantageClient>();
        using var cancellation = new CancellationTokenSource();
        var client = CreateClient(
            new ThrowingHttpMessageHandler(new OperationCanceledException("caller cancelled", cancellation.Token)), logger);
        cancellation.Cancel();

        // This test intentionally supplies a cancelled caller token rather than the test-run token.
#pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetDailySeries("AAPL", "US0378331005", _start, _end, _usd, cancellation.Token));
#pragma warning restore xUnit1051

        Assert.Contains(LogLevel.Debug, logger.Levels);
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
    }

    [Fact]
    public async Task GetDailySeries_WhenHttpTimesOut_ReturnsEmptyAndLogsDebug()
    {
        var logger = new ListLogger<AlphaVantageClient>();
        var client = CreateClient(new ThrowingHttpMessageHandler(new TaskCanceledException("request timeout")), logger);

        var result = await client.GetDailySeries(
            "AAPL", "US0378331005", _start, _end, _usd, TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Contains(LogLevel.Debug, logger.Levels);
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
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

    private sealed class MockHttpMessageHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string response = "",
        IReadOnlyList<string>? responses = null) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses ?? [response]);
        public Uri? LastRequestUri { get; private set; }
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(_responses.Count > 1 ? _responses.Dequeue() : _responses.Peek())
            });
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }
}