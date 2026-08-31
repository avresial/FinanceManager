using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using ServiceDefaults;
using System.Globalization;
using System.Net;

namespace FinanceManager.Tests.Unit.ServiceDefaults;

[Trait("Category", "Unit")]
public class ExternalDependencyLoggingHandlerTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task SendAsync_NonSuccessResponse_LogsMappedServiceWithoutQueryString(HttpStatusCode statusCode)
    {
        var logger = new RecordingLogger<ExternalDependencyLoggingHandler>();
        using var handler = new ExternalDependencyLoggingHandler(logger)
        {
            InnerHandler = new StubHandler(_ => new HttpResponseMessage(statusCode)
            {
                ReasonPhrase = statusCode.ToString()
            })
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync(
            "https://api.twelvedata.com/time_series?apikey=secret-value&symbol=IBM",
            TestContext.Current.CancellationToken);

        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Contains("Twelve Data", log.Message);
        Assert.Contains("/time_series", log.Message);
        Assert.Contains(((int)statusCode).ToString(), log.Message);
        Assert.DoesNotContain("secret-value", log.Message);
    }

    [Fact]
    public async Task SendAsync_ExpectedNotFound_IsNotPersistedAsWarning()
    {
        var logger = new RecordingLogger<ExternalDependencyLoggingHandler>();
        using var handler = new ExternalDependencyLoggingHandler(logger)
        {
            InnerHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync(
            "https://api.nbp.pl/api/exchangerates/rates/a/usd/2024-01-01",
            TestContext.Current.CancellationToken);

        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("NBP", log.Message);
        Assert.Contains("404", log.Message);
    }

    [Theory]
    [InlineData("https://eodhd.com/api/eod/AAPL.US")]
    [InlineData("https://api.eodhd.com/api/eod/AAPL.US")]
    public async Task SendAsync_EodhdHosts_AreMappedToEodhd(string uri)
    {
        var logger = new RecordingLogger<ExternalDependencyLoggingHandler>();
        using var handler = new ExternalDependencyLoggingHandler(logger)
        {
            InnerHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest))
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync(uri, TestContext.Current.CancellationToken);

        var log = Assert.Single(logger.Entries);
        Assert.Contains("Service: EODHD", log.Message);
    }

    [Fact]
    public async Task SendAsync_TransportFailure_LogsMappedServiceAndException()
    {
        var logger = new RecordingLogger<ExternalDependencyLoggingHandler>();
        var expectedException = new HttpRequestException(
            "Request https://api.openfigi.com/v3/mapping?apiKey=secret-value&token=another-secret failed with Authorization: Bearer bearer-secret");
        using var handler = new ExternalDependencyLoggingHandler(logger)
        {
            InnerHandler = new StubHandler(_ => throw expectedException)
        };
        using var client = new HttpClient(handler);

        var actualException = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync(
                "https://api.openfigi.com/v3/mapping?apiKey=secret-value",
                TestContext.Current.CancellationToken));

        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Contains("OpenFIGI", log.Message);
        Assert.Contains("/v3/mapping", log.Message);
        Assert.DoesNotContain("secret-value", log.Message);
        Assert.NotNull(log.Exception);
        Assert.Contains("HttpRequestException", log.Exception!.ToString());
        Assert.DoesNotContain("secret-value", log.Exception.ToString());
        Assert.DoesNotContain("another-secret", log.Exception.ToString());
        Assert.DoesNotContain("bearer-secret", log.Exception.ToString());
        Assert.NotSame(actualException, log.Exception);
    }

    [Fact]
    public async Task AddServiceDefaults_HttpClientFactory_UsesExternalDependencyLoggingHandler()
    {
        var entries = new List<LogRecord>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RecordingLoggerProvider(entries));
        builder.AddServiceDefaults();
        builder.Services.AddHttpClient("external")
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)));

        using var host = builder.Build();
        var clientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient("external");

        using var response = await client.GetAsync(
            "https://api.twelvedata.com/time_series?apikey=secret-value",
            TestContext.Current.CancellationToken);

        var log = Assert.Single(entries, entry => entry.Message.Contains("External dependency request", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Contains("Service: Twelve Data", log.Message);
        Assert.Contains("Path: /time_series", log.Message);
        Assert.DoesNotContain("secret-value", log.Message);
    }

    [Fact]
    public async Task AddServiceDefaults_HttpClientFactory_RetryLogIdentifiesProvider()
    {
        var entries = new List<LogRecord>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RecordingLoggerProvider(entries));
        builder.AddServiceDefaults();
        builder.Services.AddHttpClient("external")
            .ConfigurePrimaryHttpMessageHandler(() => new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.GatewayTimeout),
                new HttpResponseMessage(HttpStatusCode.OK)));

        using var host = builder.Build();
        var clientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient("external");

        using var response = await client.GetAsync(
            "https://api.twelvedata.com/time_series?apikey=secret-value",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var retryLog = Assert.Single(entries, entry => entry.Message.StartsWith("External dependency retry", StringComparison.Ordinal));
        Assert.Contains("Service: Twelve Data", retryLog.Message);
        Assert.Contains("Path: /time_series", retryLog.Message);
        Assert.Contains("Result: 504", retryLog.Message);
        Assert.DoesNotContain("secret-value", retryLog.Message);
    }

    [Fact]
    public async Task AddServiceDefaults_HttpClientFactory_ExponentialRetryDelaysStayWithinMaximum()
    {
        var entries = new List<LogRecord>();
        var timeProvider = new RetryImmediateTimeProvider();
        var primaryHandler = new CountingResponseHandler(HttpStatusCode.ServiceUnavailable);
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["HttpClientResilience:AttemptTimeoutSeconds"] = "120";
        builder.Configuration["HttpClientResilience:TotalRequestTimeoutSeconds"] = "300";
        builder.Configuration["HttpClientResilience:MaxRetryAttempts"] = "3";
        builder.Configuration["HttpClientResilience:RetryDelaySeconds"] = "10";
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RecordingLoggerProvider(entries));
        builder.Services.AddSingleton<TimeProvider>(timeProvider);
        builder.AddServiceDefaults();
        builder.Services.PostConfigureAll<HttpStandardResilienceOptions>(options =>
            options.Retry.Randomizer = () => 0.5);
        builder.Services.AddHttpClient("external", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        using var host = builder.Build();
        var clientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient("external");

        using var response = await client.GetAsync(
            "https://api.twelvedata.com/time_series?apikey=secret-value",
            TestContext.Current.CancellationToken);

        var retryLogs = entries
            .Where(entry => entry.Message.StartsWith("External dependency retry", StringComparison.Ordinal))
            .ToList();
        var retryDelays = retryLogs.Select(ParseRetryDelayMilliseconds).ToList();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(4, primaryHandler.CallCount);
        Assert.Equal(3, retryLogs.Count);
        Assert.All(retryDelays, delay => Assert.InRange(delay, 0m, 10_000m));
        Assert.Contains(10_000m, retryDelays);
    }

    [Fact]
    public async Task AddServiceDefaults_HttpClientFactory_DoesNotRetryUnsafeMethods()
    {
        var entries = new List<LogRecord>();
        var primaryHandler = new CountingResponseHandler(HttpStatusCode.ServiceUnavailable);
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["HttpClientResilience:MaxRetryAttempts"] = "5";
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RecordingLoggerProvider(entries));
        builder.AddServiceDefaults();
        builder.Services.AddHttpClient("external")
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        using var host = builder.Build();
        var clientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient("external");

        using var response = await client.PostAsync(
            "https://api.twelvedata.com/time_series?apikey=secret-value",
            new StringContent("request-body"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, primaryHandler.CallCount);
        Assert.DoesNotContain(
            entries,
            entry => entry.Message.StartsWith("External dependency retry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddServiceDefaults_HttpClientFactory_PollyRecordsAreReplacedBySafeRequestRecords()
    {
        var entries = new List<LogRecord>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RecordingLoggerProvider(entries));
        builder.AddServiceDefaults();
        builder.Services.AddHttpClient("external")
            .ConfigurePrimaryHttpMessageHandler(() => new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.GatewayTimeout),
                new HttpResponseMessage(HttpStatusCode.OK)));

        using var host = builder.Build();
        var clientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient("external");

        using var response = await client.GetAsync(
            "https://api.twelvedata.com/time_series?apikey=secret-value",
            TestContext.Current.CancellationToken);

        var resilienceLogs = entries
            .Where(entry => entry.Message.StartsWith("Execution attempt.", StringComparison.Ordinal)
                || entry.Message.StartsWith("Resilience event occurred.", StringComparison.Ordinal))
            .ToList();
        var retryLog = Assert.Single(
            entries,
            entry => entry.Message.StartsWith("External dependency retry", StringComparison.Ordinal));

        Assert.Empty(resilienceLogs);
        Assert.Contains("Operation: Twelve Data GET api.twelvedata.com", retryLog.Message);
        Assert.Contains("Service: Twelve Data", retryLog.Message);
        Assert.Contains("Path: /time_series", retryLog.Message);
        Assert.DoesNotContain("secret-value", retryLog.Message);
    }

    [Fact]
    public async Task AddServiceDefaults_HttpClientFactory_Terminal504LogsSafeRequestIdentity()
    {
        var entries = new List<LogRecord>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RecordingLoggerProvider(entries));
        builder.AddServiceDefaults();
        builder.Services.AddHttpClient("external")
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.GatewayTimeout)));

        using var host = builder.Build();
        var clientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient("external");

        using var response = await client.GetAsync(
            "https://api.twelvedata.com/time_series?apikey=secret-value",
            TestContext.Current.CancellationToken);

        var terminalLog = Assert.Single(
            entries,
            entry => entry.Message.StartsWith("External dependency request returned", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Contains("Operation: Twelve Data GET api.twelvedata.com", terminalLog.Message);
        Assert.Contains("Service: Twelve Data", terminalLog.Message);
        Assert.Contains("Host: api.twelvedata.com", terminalLog.Message);
        Assert.Contains("Method: GET", terminalLog.Message);
        Assert.Contains("Path: /time_series", terminalLog.Message);
        Assert.Contains("Result: 504", terminalLog.Message);
        Assert.DoesNotContain("secret-value", terminalLog.Message);
    }

    [Fact]
    public async Task AddServiceDefaults_HttpClientFactory_TimeoutLogUsesSafeOperationKey()
    {
        var entries = new List<LogRecord>();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["HttpClientResilience:AttemptTimeoutSeconds"] = "1";
        builder.Configuration["HttpClientResilience:TotalRequestTimeoutSeconds"] = "1";
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RecordingLoggerProvider(entries));
        builder.AddServiceDefaults();
        builder.Services.AddHttpClient("external")
            .ConfigurePrimaryHttpMessageHandler(() => new CancellationAwareHandler());

        using var host = builder.Build();
        var clientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient("external");

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetAsync(
            "https://api.twelvedata.com/time_series?apikey=secret-value",
            TestContext.Current.CancellationToken));

        var timeoutLogs = entries
            .Where(entry => entry.Message.StartsWith("External dependency timeout", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(timeoutLogs);
        Assert.All(timeoutLogs, log =>
        {
            Assert.Contains("Operation: Twelve Data GET api.twelvedata.com", log.Message);
            Assert.Contains("Service: Twelve Data", log.Message);
            Assert.Contains("Path: /time_series", log.Message);
            Assert.DoesNotContain("secret-value", log.Message);
        });
    }

    [Fact]
    public async Task AddServiceDefaults_HttpClientFactory_TransportFailureRecordsRedactExceptionSecrets()
    {
        var entries = new List<LogRecord>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RecordingLoggerProvider(entries));
        builder.AddServiceDefaults();
        builder.Services.AddHttpClient("external")
            .ConfigurePrimaryHttpMessageHandler(() => new TransportFailureHandler());

        using var host = builder.Build();
        var clientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient("external");

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetAsync(
            "https://api.openfigi.com/v3/mapping?apiKey=query-secret",
            TestContext.Current.CancellationToken));

        var capturedText = string.Join(
            Environment.NewLine,
            entries.SelectMany(entry => new[] { entry.Message, entry.Exception?.ToString() ?? string.Empty }));

        Assert.False(capturedText.Contains("query-secret", StringComparison.Ordinal), capturedText);
        Assert.False(capturedText.Contains("exception-secret", StringComparison.Ordinal), capturedText);

        var resilienceLogs = entries
            .Where(entry => entry.Message.StartsWith("Execution attempt.", StringComparison.Ordinal)
                || entry.Message.StartsWith("Resilience event occurred.", StringComparison.Ordinal))
            .ToList();

        var retryLogs = entries
            .Where(entry => entry.Message.StartsWith("External dependency retry", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(resilienceLogs);
        Assert.NotEmpty(retryLogs);
        Assert.All(retryLogs, log =>
        {
            Assert.Contains("Operation: OpenFIGI GET api.openfigi.com", log.Message);
            Assert.DoesNotContain("query-secret", log.Message);
            Assert.DoesNotContain("exception-secret", log.Message);
        });
    }

    [Fact]
    public async Task SendAsync_NonSuccessResponse_RedactsSecretAssignmentsInPath()
    {
        var logger = new RecordingLogger<ExternalDependencyLoggingHandler>();
        using var handler = new ExternalDependencyLoggingHandler(logger)
        {
            InnerHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest))
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync(
            "https://api.openfigi.com/v3/mapping/token=path-secret?apiKey=query-secret",
            TestContext.Current.CancellationToken);

        var log = Assert.Single(logger.Entries);
        Assert.Contains("Path: /v3/mapping/token=[REDACTED]", log.Message);
        Assert.DoesNotContain("path-secret", log.Message);
        Assert.DoesNotContain("query-secret", log.Message);
    }

    private sealed class CancellationAwareHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class TransportFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException(
                "Request https://api.openfigi.com/v3/mapping?apiKey=exception-secret failed with Authorization: Bearer exception-secret"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responses.Dequeue());
    }

    private sealed class CountingResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class RetryImmediateTimeProvider : TimeProvider
    {
        private static readonly TimeSpan _maxImmediateDelay = TimeSpan.FromSeconds(10);

        public override ITimer CreateTimer(
            TimerCallback? callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period) =>
            new Timer(
                callback ?? IgnoreTimerCallback,
                state,
                dueTime >= TimeSpan.Zero && dueTime <= _maxImmediateDelay ? TimeSpan.Zero : dueTime,
                period);

        private static void IgnoreTimerCallback(object? state)
        {
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public RecordingLogger(List<LogRecord>? entries = null) => Entries = entries ?? [];

        public List<LogRecord> Entries { get; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogRecord(logLevel, formatter(state, exception), exception));
    }

    private sealed class RecordingLoggerProvider(List<LogRecord> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new RecordingLogger<ExternalDependencyLoggingHandler>(entries);

        public void Dispose()
        {
        }
    }

    private sealed record LogRecord(LogLevel Level, string Message, Exception? Exception);

    private static decimal ParseRetryDelayMilliseconds(LogRecord entry)
    {
        const string prefix = "Retry Delay: ";
        const string suffix = "ms.";
        var start = entry.Message.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        var end = entry.Message.IndexOf(suffix, start, StringComparison.Ordinal);
        return decimal.Parse(entry.Message[start..end], CultureInfo.InvariantCulture);
    }
}