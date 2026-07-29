using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Services;

[Trait("Category", "Unit")]
public class LiabilitiesSnapshotStoreTests
{
    private const string _timeSeriesKey = "liabilities-timeseries:1:PLN";
    private const string _distributionKey = "liabilities-distribution:1:2";

    private static readonly DateTime _start = new(2024, 1, 1);
    private static readonly DateTime _end = new(2024, 2, 1);

    private readonly Mock<ISnapshotService> _snapshots = new();

    private LiabilitiesSnapshotStore CreateStore() =>
        new(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));

    // ---- time-series surface ----

    [Fact]
    public async Task RefreshTimeSeries_ReadFailure_PaintsNothingAndLoadsFresh()
    {
        _snapshots.Setup(x => x.GetAsync<LiabilitiesTimeSeriesSnapshot>(_timeSeriesKey)).ThrowsAsync(new InvalidOperationException());

        var result = await RefreshTimeSeries(fresh: Series(1m));

        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.RemoveAsync(_timeSeriesKey), Times.Once);
    }

    [Fact]
    public async Task RefreshTimeSeries_UnchangedSeries_PaintsAndSkipsWrite()
    {
        GivenStoredTimeSeries(1m, 2m);

        var painted = false;
        var result = await RefreshTimeSeries(fresh: Series(1m, 2m), onSnapshotPainted: _ =>
        {
            painted = true;
            return Task.CompletedTask;
        });

        Assert.True(painted);
        Assert.True(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<LiabilitiesTimeSeriesSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTimeSeries_ChangedSeries_WritesUserAndCurrencyScopedKey()
    {
        GivenStoredTimeSeries(1m, 2m);

        var result = await RefreshTimeSeries(fresh: Series(1m, 3m));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_timeSeriesKey, It.Is<LiabilitiesTimeSeriesSnapshot>(
            s => s.UserId == 1 && s.Currency == "PLN" && s.StartDate == _start && s.EndDate == _end
                 && s.Series.Select(p => p.Value).SequenceEqual(new[] { 1m, 3m }))), Times.Once);
    }

    [Fact]
    public async Task RefreshTimeSeries_SnapshotFromAnotherCurrency_IsNotPainted()
    {
        // A stale entry left under this key by a different currency must never be rendered here.
        _snapshots.Setup(x => x.GetAsync<LiabilitiesTimeSeriesSnapshot>(_timeSeriesKey))
            .ReturnsAsync(new LiabilitiesTimeSeriesSnapshot { UserId = 1, Currency = "USD", Series = Points(1m, 2m) });

        var painted = false;
        var result = await RefreshTimeSeries(fresh: Series(1m, 2m), onSnapshotPainted: _ =>
        {
            painted = true;
            return Task.CompletedTask;
        });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task RefreshTimeSeries_FetchFailureBehindSnapshot_KeepsPaintedSnapshot()
    {
        GivenStoredTimeSeries(1m, 2m);
        var store = CreateStore();

        var result = await store.RefreshTimeSeriesAsync(1, "PLN", _start, _end, new RefreshVersionGate(), claimedVersion: null,
            fetchAsync: () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<LiabilitiesTimeSeriesSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTimeSeries_SupersededBeforeCommit_DoesNotWrite()
    {
        GivenStoredTimeSeries(1m, 2m);
        var gate = new RefreshVersionGate();
        var store = CreateStore();

        var result = await store.RefreshTimeSeriesAsync(1, "PLN", _start, _end, gate, claimedVersion: null,
            fetchAsync: () =>
            {
                // A newer reload claims the gate mid-flight; this run must commit nothing.
                gate.Claim();
                return Task.FromResult<TimeSeriesCardModel?>(Series(9m));
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<LiabilitiesTimeSeriesSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTimeSeries_ForwardsClaimedVersion_CallerGuardStaysCurrent()
    {
        // Regression: the card pre-claims a version and guards its post-run state with IsCurrent(claimedVersion).
        // If the store let the coordinator claim a *newer* version instead of forwarding the pre-claimed one,
        // that guard would never match and the card's error/loading commit would be skipped.
        GivenStoredTimeSeries(1m, 2m);
        var gate = new RefreshVersionGate();
        var claimed = gate.Claim();

        await CreateStore().RefreshTimeSeriesAsync(1, "PLN", _start, _end, gate, claimed,
            () => Task.FromResult<TimeSeriesCardModel?>(Series(1m, 3m)));

        Assert.True(gate.IsCurrent(claimed));
    }

    // ---- distribution surface ----

    [Fact]
    public async Task RefreshDistribution_ChangedData_WritesUserAndCurrencyScopedKey()
    {
        _snapshots.Setup(x => x.GetAsync<LiabilitiesDistributionSnapshot>(_distributionKey))
            .ReturnsAsync(new LiabilitiesDistributionSnapshot { UserId = 1, CurrencyId = 2, TypeData = Values(("Loan", 5m)), AccountData = [] });

        var result = await RefreshDistribution(fresh: new DistributionCardModel(Values(("Loan", 7m)), []));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_distributionKey, It.Is<LiabilitiesDistributionSnapshot>(
            s => s.UserId == 1 && s.CurrencyId == 2 && s.TypeData.Single().Value == 7m)), Times.Once);
    }

    [Fact]
    public async Task RefreshDistribution_UnchangedData_SkipsWrite()
    {
        _snapshots.Setup(x => x.GetAsync<LiabilitiesDistributionSnapshot>(_distributionKey))
            .ReturnsAsync(new LiabilitiesDistributionSnapshot { UserId = 1, CurrencyId = 2, TypeData = Values(("Loan", 5m)), AccountData = Values(("Acc", 5m)) });

        var result = await RefreshDistribution(fresh: new DistributionCardModel(Values(("Loan", 5m)), Values(("Acc", 5m))));

        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<LiabilitiesDistributionSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshDistribution_SnapshotFromAnotherUser_IsNotPainted()
    {
        _snapshots.Setup(x => x.GetAsync<LiabilitiesDistributionSnapshot>(_distributionKey))
            .ReturnsAsync(new LiabilitiesDistributionSnapshot { UserId = 99, CurrencyId = 2, TypeData = Values(("Loan", 5m)), AccountData = [] });

        var painted = false;
        var result = await RefreshDistribution(fresh: new DistributionCardModel(Values(("Loan", 5m)), []), onSnapshotPainted: _ =>
        {
            painted = true;
            return Task.CompletedTask;
        });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task RefreshDistribution_FetchFailureBehindSnapshot_KeepsPaintedSnapshot()
    {
        _snapshots.Setup(x => x.GetAsync<LiabilitiesDistributionSnapshot>(_distributionKey))
            .ReturnsAsync(new LiabilitiesDistributionSnapshot { UserId = 1, CurrencyId = 2, TypeData = Values(("Loan", 5m)), AccountData = [] });
        var store = CreateStore();

        var result = await store.RefreshDistributionAsync(1, 2, _start, _end, new RefreshVersionGate(), claimedVersion: null,
            fetchAsync: () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<LiabilitiesDistributionSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshDistribution_SupersededBeforeCommit_DoesNotWrite()
    {
        _snapshots.Setup(x => x.GetAsync<LiabilitiesDistributionSnapshot>(_distributionKey))
            .ReturnsAsync(new LiabilitiesDistributionSnapshot { UserId = 1, CurrencyId = 2, TypeData = Values(("Loan", 5m)), AccountData = [] });
        var gate = new RefreshVersionGate();
        var store = CreateStore();

        var result = await store.RefreshDistributionAsync(1, 2, _start, _end, gate, claimedVersion: null,
            fetchAsync: () =>
            {
                // A newer reload claims the gate mid-flight; this run must commit nothing.
                gate.Claim();
                return Task.FromResult<DistributionCardModel?>(new DistributionCardModel(Values(("Loan", 9m)), []));
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<LiabilitiesDistributionSnapshot>()), Times.Never);
    }

    // ---- helpers ----

    private void GivenStoredTimeSeries(params decimal[] values) =>
        _snapshots.Setup(x => x.GetAsync<LiabilitiesTimeSeriesSnapshot>(_timeSeriesKey))
            .ReturnsAsync(new LiabilitiesTimeSeriesSnapshot { UserId = 1, Currency = "PLN", StartDate = _start, EndDate = _end, Series = Points(values) });

    private Task<SnapshotRefreshResult<TimeSeriesCardModel>> RefreshTimeSeries(
        TimeSeriesCardModel? fresh,
        Func<TimeSeriesCardModel, Task>? onSnapshotPainted = null) =>
        CreateStore().RefreshTimeSeriesAsync(1, "PLN", _start, _end, new RefreshVersionGate(), claimedVersion: null,
            () => Task.FromResult(fresh), onSnapshotPainted);

    private Task<SnapshotRefreshResult<DistributionCardModel>> RefreshDistribution(
        DistributionCardModel? fresh,
        Func<DistributionCardModel, Task>? onSnapshotPainted = null) =>
        CreateStore().RefreshDistributionAsync(1, 2, _start, _end, new RefreshVersionGate(), claimedVersion: null,
            () => Task.FromResult(fresh), onSnapshotPainted);

    private static TimeSeriesCardModel Series(params decimal[] values) => new(Points(values));

    private static List<TimeSeriesModel> Points(params decimal[] values) =>
        [.. values.Select((v, i) => new TimeSeriesModel(_start.AddDays(i), v))];

    private static List<NameValueResult> Values(params (string Name, decimal Value)[] entries) =>
        [.. entries.Select(e => new NameValueResult(e.Name, e.Value))];
}