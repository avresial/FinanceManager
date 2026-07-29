using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Insights.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Services;

[Trait("Category", "Unit")]
public class DiversificationSnapshotStoreTests
{
    private static readonly DateTime _asOfDate = new(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
    private const string _key = "diversification-score:1:2026-07-29";
    private readonly Mock<ISnapshotService> _snapshots = new();

    private DiversificationSnapshotStore CreateStore() =>
        new(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));

    [Fact]
    public async Task Unchanged_PaintsBeforeFetch_AndSkipsWrite()
    {
        _snapshots.Setup(x => x.GetAsync<DiversificationScoreSnapshot>(_key))
            .ReturnsAsync(Snapshot(Score(50)));
        var painted = false;
        var fetchedAfterPaint = false;

        var result = await Refresh(Score(50), score =>
        {
            painted = score.Score == 50;
            return Task.CompletedTask;
        }, () => fetchedAfterPaint = painted);

        Assert.True(fetchedAfterPaint);
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<DiversificationScoreSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task Changed_WritesUserAndDayScopedSnapshot()
    {
        var result = await Refresh(Score(67));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<DiversificationScoreSnapshot>(
            snapshot => snapshot.UserId == 1
                && snapshot.AsOfDate == _asOfDate
                && snapshot.Score.Score == 67)), Times.Once);
    }

    [Fact]
    public async Task SnapshotFromAnotherDay_IsNotPainted()
    {
        _snapshots.Setup(x => x.GetAsync<DiversificationScoreSnapshot>(_key))
            .ReturnsAsync(Snapshot(Score(50), _asOfDate.AddDays(-1)));
        var painted = false;

        var result = await Refresh(Score(50), _ =>
        {
            painted = true;
            return Task.CompletedTask;
        });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task FailedRefresh_KeepsPaintedScore()
    {
        _snapshots.Setup(x => x.GetAsync<DiversificationScoreSnapshot>(_key))
            .ReturnsAsync(Snapshot(Score(50)));

        var result = await CreateStore().RefreshAsync(
            1,
            _asOfDate,
            new RefreshVersionGate(),
            null,
            () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
    }

    [Fact]
    public async Task SupersededRefresh_DoesNotWrite()
    {
        var gate = new RefreshVersionGate();

        var result = await CreateStore().RefreshAsync(
            1,
            _asOfDate,
            gate,
            null,
            () =>
            {
                gate.Claim();
                return Task.FromResult<DiversificationScore?>(Score(67));
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<DiversificationScoreSnapshot>()), Times.Never);
    }

    private Task<SnapshotRefreshResult<DiversificationScore>> Refresh(
        DiversificationScore? fresh,
        Func<DiversificationScore, Task>? onSnapshotPainted = null,
        Action? onFetch = null) =>
        CreateStore().RefreshAsync(
            1,
            _asOfDate,
            new RefreshVersionGate(),
            null,
            () =>
            {
                onFetch?.Invoke();
                return Task.FromResult(fresh);
            },
            onSnapshotPainted);

    private static DiversificationScoreSnapshot Snapshot(DiversificationScore score, DateTime? asOfDate = null) =>
        new() { UserId = 1, AsOfDate = asOfDate ?? _asOfDate, Score = score };

    private static DiversificationScore Score(int score) =>
        new(score, score / 2, score - (score / 2), score >= 67 ? "Broad" : "Moderate");
}