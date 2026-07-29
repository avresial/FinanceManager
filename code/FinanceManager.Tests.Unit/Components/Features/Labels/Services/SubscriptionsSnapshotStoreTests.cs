using FinanceManager.Components.Features.Labels.Models;
using FinanceManager.Components.Features.Labels.Services;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Labels.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.Labels.Services;

[Trait("Category", "Unit")]
public class SubscriptionsSnapshotStoreTests
{
    private const string _summaryKey = "subscriptions-summary:1:PLN";
    private const string _listKey = "subscriptions-list:1:PLN";

    private static readonly DateTime _nextCharge = new(2026, 8, 1);

    private readonly Mock<ISnapshotService> _snapshots = new();

    private SubscriptionsSnapshotStore CreateStore() =>
        new(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));

    // ---- summary surface ----

    [Fact]
    public async Task RefreshSummary_StoredTotals_ArePaintedBeforeTheRequestRuns()
    {
        GivenStoredSummary(activeCount: 2, monthlyCost: 30m, increaseCount: 1);

        SubscriptionsSummaryModel? painted = null;
        var fetchStarted = false;

        await CreateStore().RefreshSummaryAsync(1, "PLN", new RefreshVersionGate(), claimedVersion: null,
            fetchAsync: () =>
            {
                fetchStarted = true;
                // The snapshot must already be on screen by the time the fresh request starts.
                Assert.NotNull(painted);
                return Task.FromResult<SubscriptionsSummaryModel?>(new(3, 45m, 1));
            },
            onSnapshotPainted: model =>
            {
                painted = model;
                return Task.CompletedTask;
            });

        Assert.True(fetchStarted);
        Assert.Equal(new SubscriptionsSummaryModel(2, 30m, 1), painted);
    }

    [Fact]
    public async Task RefreshSummary_UnchangedTotals_PaintsAndSkipsWrite()
    {
        GivenStoredSummary(activeCount: 2, monthlyCost: 30m, increaseCount: 1);

        var result = await RefreshSummary(fresh: new(2, 30m, 1));

        Assert.True(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SubscriptionsSummarySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshSummary_ChangedTotals_WritesUserAndCurrencyScopedKey()
    {
        GivenStoredSummary(activeCount: 2, monthlyCost: 30m, increaseCount: 1);

        var result = await RefreshSummary(fresh: new(3, 45m, 0));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_summaryKey, It.Is<SubscriptionsSummarySnapshot>(
            s => s.UserId == 1 && s.Currency == "PLN"
                 && s.ActiveCount == 3 && s.MonthlyCost == 45m && s.IncreaseCount == 0)), Times.Once);
    }

    [Fact]
    public async Task RefreshSummary_NoSubscriptionsLeft_ClearsTheStoredTotals()
    {
        // An empty model is a real result, not a failure: it must replace populated totals.
        GivenStoredSummary(activeCount: 2, monthlyCost: 30m, increaseCount: 1);

        var result = await RefreshSummary(fresh: SubscriptionsSummaryModel.Empty);

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_summaryKey, It.Is<SubscriptionsSummarySnapshot>(
            s => s.ActiveCount == 0 && s.MonthlyCost == 0m && s.IncreaseCount == 0)), Times.Once);
    }

    [Fact]
    public async Task RefreshSummary_SnapshotFromAnotherCurrency_IsNotPainted()
    {
        _snapshots.Setup(x => x.GetAsync<SubscriptionsSummarySnapshot>(_summaryKey))
            .ReturnsAsync(new SubscriptionsSummarySnapshot { UserId = 1, Currency = "USD", ActiveCount = 2, MonthlyCost = 30m });

        var result = await RefreshSummary(fresh: new(2, 30m, 0));

        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task RefreshSummary_SnapshotFromAnotherUser_IsNotPainted()
    {
        _snapshots.Setup(x => x.GetAsync<SubscriptionsSummarySnapshot>(_summaryKey))
            .ReturnsAsync(new SubscriptionsSummarySnapshot { UserId = 99, Currency = "PLN", ActiveCount = 2, MonthlyCost = 30m });

        var result = await RefreshSummary(fresh: new(2, 30m, 0));

        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task RefreshSummary_FetchFailureBehindSnapshot_KeepsPaintedSnapshot()
    {
        GivenStoredSummary(activeCount: 2, monthlyCost: 30m, increaseCount: 1);

        var result = await CreateStore().RefreshSummaryAsync(1, "PLN", new RefreshVersionGate(), claimedVersion: null,
            fetchAsync: () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SubscriptionsSummarySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshSummary_FetchFailureWithoutSnapshot_IsBlocking()
    {
        var result = await CreateStore().RefreshSummaryAsync(1, "PLN", new RefreshVersionGate(), claimedVersion: null,
            fetchAsync: () => throw new HttpRequestException());

        Assert.True(result.IsBlockingFailure);
    }

    [Fact]
    public async Task RefreshSummary_SupersededBeforeCommit_DoesNotWrite()
    {
        GivenStoredSummary(activeCount: 2, monthlyCost: 30m, increaseCount: 1);
        var gate = new RefreshVersionGate();

        var result = await CreateStore().RefreshSummaryAsync(1, "PLN", gate, claimedVersion: null,
            fetchAsync: () =>
            {
                // A newer reload claims the gate mid-flight; this run must commit nothing.
                gate.Claim();
                return Task.FromResult<SubscriptionsSummaryModel?>(new(9, 99m, 9));
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SubscriptionsSummarySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshSummary_ForwardsClaimedVersion_CallerGuardStaysCurrent()
    {
        // The page pre-claims one version for both surfaces and guards its post-run state with it.
        // Letting the coordinator claim a newer version would make the two surfaces supersede each other.
        GivenStoredSummary(activeCount: 2, monthlyCost: 30m, increaseCount: 1);
        var gate = new RefreshVersionGate();
        var claimed = gate.Claim();

        await CreateStore().RefreshSummaryAsync(1, "PLN", gate, claimed,
            () => Task.FromResult<SubscriptionsSummaryModel?>(new(3, 45m, 0)));

        Assert.True(gate.IsCurrent(claimed));
    }

    // ---- list surface ----

    [Fact]
    public async Task RefreshList_ReadFailure_PaintsNothingAndLoadsFresh()
    {
        _snapshots.Setup(x => x.GetAsync<SubscriptionsListSnapshot>(_listKey)).ThrowsAsync(new InvalidOperationException());

        var result = await RefreshList(fresh: [Subscription("Netflix", 30m)]);

        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.RemoveAsync(_listKey), Times.Once);
    }

    [Fact]
    public async Task RefreshList_UnchangedSubscriptions_PaintsAndSkipsWrite()
    {
        GivenStoredList(Subscription("Netflix", 30m));

        var result = await RefreshList(fresh: [Subscription("Netflix", 30m)]);

        Assert.True(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SubscriptionsListSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshList_ChangedSubscriptions_WritesUserAndCurrencyScopedKey()
    {
        GivenStoredList(Subscription("Netflix", 30m));

        var result = await RefreshList(fresh: [Subscription("Netflix", 35m)]);

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_listKey, It.Is<SubscriptionsListSnapshot>(
            s => s.UserId == 1 && s.Currency == "PLN" && s.Subscriptions.Single().MonthlyCost == 35m)), Times.Once);
    }

    [Fact]
    public async Task RefreshList_MutedSubscription_IsStoredSoTheFilterCanStillShowIt()
    {
        // "Show muted and cancelled" filters the rendered list, so inactive items must survive the snapshot.
        var muted = Subscription("Netflix", 30m);
        muted.IsMuted = true;

        var result = await RefreshList(fresh: [muted]);

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_listKey, It.Is<SubscriptionsListSnapshot>(
            s => s.Subscriptions.Single().IsMuted)), Times.Once);
    }

    [Fact]
    public async Task RefreshList_NoSubscriptionsLeft_ClearsTheStoredList()
    {
        GivenStoredList(Subscription("Netflix", 30m));

        var result = await RefreshList(fresh: []);

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_listKey, It.Is<SubscriptionsListSnapshot>(
            s => s.Subscriptions.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task RefreshList_SnapshotFromAnotherUser_IsNotPainted()
    {
        _snapshots.Setup(x => x.GetAsync<SubscriptionsListSnapshot>(_listKey))
            .ReturnsAsync(new SubscriptionsListSnapshot { UserId = 99, Currency = "PLN", Subscriptions = [Subscription("Netflix", 30m)] });

        var result = await RefreshList(fresh: [Subscription("Netflix", 30m)]);

        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task RefreshList_FetchFailureBehindSnapshot_KeepsPaintedSnapshot()
    {
        GivenStoredList(Subscription("Netflix", 30m));

        var result = await CreateStore().RefreshListAsync(1, "PLN", new RefreshVersionGate(), claimedVersion: null,
            fetchAsync: () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SubscriptionsListSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshList_SupersededBeforeCommit_DoesNotWrite()
    {
        GivenStoredList(Subscription("Netflix", 30m));
        var gate = new RefreshVersionGate();

        var result = await CreateStore().RefreshListAsync(1, "PLN", gate, claimedVersion: null,
            fetchAsync: () =>
            {
                gate.Claim();
                return Task.FromResult<List<RecurringTransactionResult>?>([Subscription("Spotify", 20m)]);
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SubscriptionsListSnapshot>()), Times.Never);
    }

    // ---- surface isolation ----

    [Fact]
    public async Task Surfaces_UseSeparateKeys_SoNeitherOverwritesTheOther()
    {
        var store = CreateStore();

        await store.RefreshSummaryAsync(1, "PLN", new RefreshVersionGate(), claimedVersion: null,
            () => Task.FromResult<SubscriptionsSummaryModel?>(new(1, 30m, 0)));
        await store.RefreshListAsync(1, "PLN", new RefreshVersionGate(), claimedVersion: null,
            () => Task.FromResult<List<RecurringTransactionResult>?>([Subscription("Netflix", 30m)]));

        _snapshots.Verify(x => x.SetAsync(_summaryKey, It.IsAny<SubscriptionsSummarySnapshot>()), Times.Once);
        _snapshots.Verify(x => x.SetAsync(_listKey, It.IsAny<SubscriptionsListSnapshot>()), Times.Once);
        _snapshots.Verify(x => x.GetAsync<SubscriptionsSummarySnapshot>(_listKey), Times.Never);
        _snapshots.Verify(x => x.GetAsync<SubscriptionsListSnapshot>(_summaryKey), Times.Never);
    }

    [Fact]
    public async Task Surfaces_ScopeKeysPerUserAndCurrency()
    {
        var store = CreateStore();

        await store.RefreshSummaryAsync(7, "USD", new RefreshVersionGate(), claimedVersion: null,
            () => Task.FromResult<SubscriptionsSummaryModel?>(new(1, 30m, 0)));
        await store.RefreshListAsync(7, "USD", new RefreshVersionGate(), claimedVersion: null,
            () => Task.FromResult<List<RecurringTransactionResult>?>([Subscription("Netflix", 30m)]));

        _snapshots.Verify(x => x.SetAsync("subscriptions-summary:7:USD", It.IsAny<SubscriptionsSummarySnapshot>()), Times.Once);
        _snapshots.Verify(x => x.SetAsync("subscriptions-list:7:USD", It.IsAny<SubscriptionsListSnapshot>()), Times.Once);
    }

    // ---- helpers ----

    private void GivenStoredSummary(int activeCount, decimal monthlyCost, int increaseCount) =>
        _snapshots.Setup(x => x.GetAsync<SubscriptionsSummarySnapshot>(_summaryKey))
            .ReturnsAsync(new SubscriptionsSummarySnapshot
            {
                UserId = 1,
                Currency = "PLN",
                ActiveCount = activeCount,
                MonthlyCost = monthlyCost,
                IncreaseCount = increaseCount
            });

    private void GivenStoredList(params RecurringTransactionResult[] subscriptions) =>
        _snapshots.Setup(x => x.GetAsync<SubscriptionsListSnapshot>(_listKey))
            .ReturnsAsync(new SubscriptionsListSnapshot { UserId = 1, Currency = "PLN", Subscriptions = [.. subscriptions] });

    private Task<SnapshotRefreshResult<SubscriptionsSummaryModel>> RefreshSummary(
        SubscriptionsSummaryModel? fresh,
        Func<SubscriptionsSummaryModel, Task>? onSnapshotPainted = null) =>
        CreateStore().RefreshSummaryAsync(1, "PLN", new RefreshVersionGate(), claimedVersion: null,
            () => Task.FromResult(fresh), onSnapshotPainted);

    private Task<SnapshotRefreshResult<List<RecurringTransactionResult>>> RefreshList(
        List<RecurringTransactionResult>? fresh,
        Func<List<RecurringTransactionResult>, Task>? onSnapshotPainted = null) =>
        CreateStore().RefreshListAsync(1, "PLN", new RefreshVersionGate(), claimedVersion: null,
            () => Task.FromResult(fresh), onSnapshotPainted);

    private static RecurringTransactionResult Subscription(string name, decimal monthlyCost) =>
        new(name, monthlyCost)
        {
            PatternId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            MonthlyCost = monthlyCost,
            AnnualCost = monthlyCost * 12m,
            LastAmount = monthlyCost,
            PreviousAmount = monthlyCost,
            NextExpectedChargeDate = _nextCharge
        };
}