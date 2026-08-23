using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Services;

[Trait("Category", "Unit")]
public class InvestmentChartRequestLoaderTests
{
    [Fact]
    public async Task LoadAsync_StartsEveryRequestBeforeAwaitingAnyResult()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        async Task<T> WaitForRelease<T>(T value)
        {
            if (Interlocked.Increment(ref started) == 4)
                allStarted.TrySetResult(true);

            await release.Task;
            return value;
        }

        var loadTask = InvestmentChartRequestLoader.LoadAsync(
            () => WaitForRelease<IReadOnlyDictionary<DateTime, decimal>>(new Dictionary<DateTime, decimal>()),
            () => WaitForRelease<IReadOnlyDictionary<long, decimal>>(new Dictionary<long, decimal>()),
            () => WaitForRelease<IReadOnlyList<UnrealizedGainLossAccountResult>>(new List<UnrealizedGainLossAccountResult>()),
            () => WaitForRelease<IReadOnlyDictionary<DateTime, decimal>>(new Dictionary<DateTime, decimal>()));

        await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.False(loadTask.IsCompleted);

        release.SetResult(true);
        var result = await loadTask;

        Assert.Empty(result.Series);
        Assert.Empty(result.Holdings);
        Assert.Empty(result.Appreciation);
        Assert.Empty(result.BenchmarkSeries);
    }
}