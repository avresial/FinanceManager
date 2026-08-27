using FinanceManager.Application.Backfill.Currencies;

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
        var source = new FallbackFxDailySource([secondary, primary]);

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
        var source = new FallbackFxDailySource([secondary, primary]);

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
        var source = new FallbackFxDailySource([primary, secondary]);

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
        var source = new FallbackFxDailySource([primary, secondary]);

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
        var source = new FallbackFxDailySource([primary, secondary]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.GetDailyRatesAsync("EUR", "USD", cts.Token));

        Assert.Equal(0, primary.CallCount);
        Assert.Equal(0, secondary.CallCount);
    }

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
}