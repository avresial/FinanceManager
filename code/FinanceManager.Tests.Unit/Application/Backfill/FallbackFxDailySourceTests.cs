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

    private sealed class FakeSource(string name, int priority, FxDailyResult result) : IFxDailySource
    {
        public string Name => name;
        public int Priority => priority;
        public int CallCount { get; private set; }

        public Task<FxDailyResult> GetDailyRatesAsync(
            string fromCurrency,
            string toCurrency,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}