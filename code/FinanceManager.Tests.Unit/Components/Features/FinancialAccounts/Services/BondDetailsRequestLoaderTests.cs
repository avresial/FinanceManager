using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using System.Collections.Concurrent;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Services;

[Trait("Category", "Unit")]
public class BondDetailsRequestLoaderTests
{
    [Fact]
    public async Task LoadAsync_OverlappingCallsShareOneRequestPerBondId()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCounts = new ConcurrentDictionary<int, int>();

        var loader = new BondDetailsRequestLoader(async bondDetailsId =>
        {
            requestCounts.AddOrUpdate(bondDetailsId, 1, (_, count) => count + 1);
            if (requestCounts.Count == 2)
                allStarted.TrySetResult(true);

            await release.Task;
            return (BondDetails?)null;
        });

        var firstLoad = Task.WhenAll(loader.LoadAsync(1), loader.LoadAsync(2));
        await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var overlappingLoad = Task.WhenAll(loader.LoadAsync(1), loader.LoadAsync(2));

        Assert.False(overlappingLoad.IsCompleted);
        Assert.Equal(1, requestCounts[1]);
        Assert.Equal(1, requestCounts[2]);

        release.SetResult(true);
        await Task.WhenAll(firstLoad, overlappingLoad);

        await loader.LoadAsync(1);
        await loader.LoadAsync(2);
        Assert.Equal(1, requestCounts[1]);
        Assert.Equal(1, requestCounts[2]);
    }
}