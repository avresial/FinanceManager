using FinanceManager.Api.Services;
using FinanceManager.Application.Backfill;
using FinanceManager.Application.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace FinanceManager.Tests.Unit.Api.Services;

[Trait("Category", "Unit")]
public class StartupBackfillBackgroundServiceTests
{
    [Fact]
    public async Task RunsAllRegisteredServicesInDeterministicOrder()
    {
        var log = new List<string>();
        var services = new List<IBackfillService>
        {
            new FakeBackfillService("c", 30, log),
            new FakeBackfillService("a", 10, log),
            new FakeBackfillService("b", 20, log),
        };

        await RunAsync(services, CancellationToken.None);

        Assert.Equal(["a", "b", "c"], log);
    }

    [Fact]
    public async Task EqualOrder_BreaksTieByNameAscending()
    {
        var log = new List<string>();
        // Same Order, registered in reverse-alphabetical order: the ThenBy(Name) tie-break must still
        // run them alphabetically.
        var services = new List<IBackfillService>
        {
            new FakeBackfillService("zeta", 10, log),
            new FakeBackfillService("alpha", 10, log),
        };

        await RunAsync(services, CancellationToken.None);

        Assert.Equal(["alpha", "zeta"], log);
    }

    [Fact]
    public async Task ContinuesAfterOneServiceThrows()
    {
        var log = new List<string>();
        var services = new List<IBackfillService>
        {
            new FakeBackfillService("first", 10, log),
            new FakeBackfillService("boom", 20, log, throwOnRun: new InvalidOperationException("boom")),
            new FakeBackfillService("third", 30, log),
        };

        await RunAsync(services, CancellationToken.None);

        // All three ran even though the middle one threw: failures are isolated.
        Assert.Equal(["first", "boom", "third"], log);
    }

    [Fact]
    public async Task PreCancelledToken_RunsNoServices()
    {
        var log = new List<string>();
        var services = new List<IBackfillService>
        {
            new FakeBackfillService("a", 10, log),
            new FakeBackfillService("b", 20, log),
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await RunAsync(services, cts.Token);

        Assert.Empty(log);
    }

    [Fact]
    public async Task CancellationMidRun_StopsRemainingServices()
    {
        var log = new List<string>();
        using var cts = new CancellationTokenSource();
        var services = new List<IBackfillService>
        {
            new FakeBackfillService("first", 10, log, onRun: cts.Cancel),
            new FakeBackfillService("second", 20, log),
        };

        await RunAsync(services, cts.Token);

        // The first service cancels the token; the orchestrator must stop before the second.
        Assert.Equal(["first"], log);
    }

    [Fact]
    public async Task RunOnStartupFalse_RunsNothing()
    {
        var log = new List<string>();
        var services = new List<IBackfillService> { new FakeBackfillService("a", 10, log) };

        await RunAsync(services, CancellationToken.None, runOnStartup: false);

        Assert.Empty(log);
    }

    private static async Task RunAsync(IEnumerable<IBackfillService> services, CancellationToken token, bool runOnStartup = true)
    {
        var collection = new ServiceCollection();
        foreach (var service in services)
            collection.AddSingleton(service);

        using var provider = collection.BuildServiceProvider();

        var sut = new StartupBackfillBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackfillOptions { RunOnStartup = runOnStartup }),
            NullLogger<StartupBackfillBackgroundService>.Instance);

        // ExecuteAsync is protected; invoke it directly so the test controls the stopping token.
        var execute = typeof(StartupBackfillBackgroundService)
            .GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)execute.Invoke(sut, [token])!;
    }

    private sealed class FakeBackfillService(string name, int order, List<string> log, Exception? throwOnRun = null, Action? onRun = null) : IBackfillService
    {
        public string Name => name;
        public int Order => order;

        public Task<BackfillResult> BackfillAsync(CancellationToken cancellationToken)
        {
            onRun?.Invoke();
            log.Add(name);

            if (throwOnRun is not null)
                throw throwOnRun;

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(BackfillResult.Empty);
        }
    }
}