using FinanceManager.Application.Backfill.Currencies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Unit.Application.Backfill;

[Trait("Category", "Unit")]
public class FallbackFxDailySourceTests
{
    [Fact]
    public async Task UsesConfiguredPriorityAndFallsBack()
    {
        var secondary = new FakeSource("Secondary", 200, FxDailyResult.Success(new Dictionary<DateTime, decimal>
        {
            [new DateTime(2024, 1, 1)] = 1.1m
        }));
        var primary = new FakeSource("Primary", 100, FxDailyResult.Empty);
        var source = Create(secondary, primary);

        var result = await source.GetDailyRatesAsync("EUR", "USD", TestContext.Current.CancellationToken);

        Assert.Equal(FxDailyStatus.Ok, result.Status);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, secondary.CallCount);
    }

    [Fact]
    public async Task FallsThroughToSecondary_WhenPrimaryFailsOrThrows()
    {
        var primary = new FakeSource("Primary", 100, FxDailyResult.Failed, throwException: true);
        var secondary = new FakeSource("Secondary", 200, FxDailyResult.Success(new Dictionary<DateTime, decimal>
        {
            [new DateTime(2024, 1, 1)] = 1.1m
        }));
        var source = Create(secondary, primary);

        var result = await source.GetDailyRatesAsync("EUR", "USD", TestContext.Current.CancellationToken);

        Assert.Equal(FxDailyStatus.Ok, result.Status);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, secondary.CallCount);
    }

    [Fact]
    public async Task ReturnsFailed_WhenAllSourcesFail()
    {
        var primary = new FakeSource("Primary", 100, FxDailyResult.Failed);
        var secondary = new FakeSource("Secondary", 200, FxDailyResult.Failed);
        var source = Create(primary, secondary);

        var result = await source.GetDailyRatesAsync("EUR", "USD", TestContext.Current.CancellationToken);

        Assert.Equal(FxDailyStatus.Error, result.Status);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, secondary.CallCount);
    }

    [Fact]
    public async Task ReturnsRateLimited_WhenPrimaryRateLimitedAndSecondaryEmpty()
    {
        var primary = new FakeSource("Primary", 100, FxDailyResult.RateLimited);
        var secondary = new FakeSource("Secondary", 200, FxDailyResult.Empty);
        var source = Create(primary, secondary);

        var result = await source.GetDailyRatesAsync("EUR", "USD", TestContext.Current.CancellationToken);

        Assert.Equal(FxDailyStatus.RateLimited, result.Status);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, secondary.CallCount);
    }

    [Fact]
    public async Task CallerCancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var primary = new FakeSource("Primary", 100, FxDailyResult.Empty);
        var secondary = new FakeSource("Secondary", 200, FxDailyResult.Empty);
        var source = Create(primary, secondary);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.GetDailyRatesAsync("EUR", "USD", cts.Token));

        Assert.Equal(0, primary.CallCount);
        Assert.Equal(0, secondary.CallCount);
    }

    [Fact]
    public async Task LogsProviderExceptionBeforeFallingBack()
    {
        var entries = new List<LogEntry>();
        var primary = new FakeSource("Primary", 100, FxDailyResult.Failed, throwException: true);
        var secondary = new FakeSource("Secondary", 200, FxDailyResult.Empty);
        var source = new FallbackFxDailySource([primary, secondary], new RecordingLogger(entries));

        await source.GetDailyRatesAsync("EUR", "USD", TestContext.Current.CancellationToken);

        var log = Assert.Single(entries);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Contains("FX source Primary failed", log.Message);
        Assert.Contains("EUR/USD", log.Message);
        Assert.IsType<HttpRequestException>(log.Exception);
    }

    private static FallbackFxDailySource Create(params IFxDailySource[] sources) =>
        new(sources, NullLogger<FallbackFxDailySource>.Instance);

    private sealed class FakeSource(
        string name,
        int priority,
        FxDailyResult result,
        bool throwException = false) : IFxDailySource
    {
        public string Name => name;
        public int Priority => priority;
        public int CallCount { get; private set; }

        public Task<FxDailyResult> GetDailyRatesAsync(
            string fromCurrency,
            string toCurrency,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (throwException)
                throw new HttpRequestException("Simulated provider failure");
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingLogger(List<LogEntry> entries) : ILogger<FallbackFxDailySource>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}