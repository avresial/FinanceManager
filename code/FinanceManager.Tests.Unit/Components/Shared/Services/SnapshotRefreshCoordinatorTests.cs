using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Shared.Services;

[Trait("Category", "Unit")]
public class SnapshotRefreshCoordinatorTests
{
    private const string _key = "test-surface:7";

    private readonly Mock<ISnapshotService> _snapshots = new();

    private sealed class TestSnapshot : SnapshotBase
    {
        public string Payload { get; set; } = string.Empty;
    }

    private sealed record TestModel(string Payload);

    private SnapshotRefreshCoordinator CreateCoordinator() =>
        new(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance);

    private void GivenStoredSnapshot(string payload) =>
        _snapshots.Setup(x => x.GetAsync<TestSnapshot>(_key)).ReturnsAsync(new TestSnapshot { Payload = payload });

    private static SnapshotRefreshRequest<TestSnapshot, TestModel> Request(
        Func<Task<TestModel?>> fetchAsync,
        Func<TestModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<TestModel, Task>? onRefreshed = null,
        RefreshVersionGate? gate = null) => new()
        {
            Key = _key,
            Gate = gate,
            ToModel = snapshot => new TestModel(snapshot.Payload),
            FetchAsync = fetchAsync,
            ToSnapshot = model => new TestSnapshot { Payload = model.Payload },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed,
        };

    [Fact]
    public async Task RunAsync_PaintsSnapshotBeforeFreshRequestCompletes()
    {
        GivenStoredSnapshot("cached");
        var fetchGate = new TaskCompletionSource<TestModel?>();
        var paintedWhileFetchPending = false;
        TestModel? painted = null;

        var run = CreateCoordinator().RunAsync(Request(
            fetchAsync: () => fetchGate.Task,
            onSnapshotPainted: model =>
            {
                painted = model;
                paintedWhileFetchPending = !fetchGate.Task.IsCompleted;
                return Task.CompletedTask;
            }));

        Assert.False(run.IsCompleted);
        Assert.Equal(new TestModel("cached"), painted);
        Assert.True(paintedWhileFetchPending);

        fetchGate.SetResult(new TestModel("cached"));
        await run;
    }

    [Fact]
    public async Task RunAsync_WithSnapshot_StillRunsFreshRequest()
    {
        GivenStoredSnapshot("cached");
        var fetchCount = 0;

        await CreateCoordinator().RunAsync(Request(fetchAsync: () =>
        {
            fetchCount++;
            return Task.FromResult<TestModel?>(new TestModel("cached"));
        }));

        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task RunAsync_WithoutSnapshot_ReportsSnapshotMissing()
    {
        var missingReported = false;

        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromResult<TestModel?>(new TestModel("fresh")),
            onSnapshotMissing: () =>
            {
                missingReported = true;
                return Task.CompletedTask;
            }));

        Assert.True(missingReported);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task RunAsync_FreshDataEqualsSnapshot_DoesNotRepaintOrWrite()
    {
        GivenStoredSnapshot("same");
        var repainted = false;

        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromResult<TestModel?>(new TestModel("same")),
            onRefreshed: _ =>
            {
                repainted = true;
                return Task.CompletedTask;
            }));

        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        Assert.Equal(new TestModel("same"), result.Model);
        Assert.False(repainted);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TestSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_SnapshotMetadataDiffers_CountsAsUnchanged()
    {
        // The stored snapshot was captured a week ago; only the rendered payload may decide equality.
        _snapshots.Setup(x => x.GetAsync<TestSnapshot>(_key))
            .ReturnsAsync(new TestSnapshot { Payload = "same", FetchedAtUtc = DateTime.UtcNow.AddDays(-7) });

        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromResult<TestModel?>(new TestModel("same"))));

        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TestSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_FreshDataDiffers_ReplacesModelAndSnapshot()
    {
        GivenStoredSnapshot("cached");
        TestModel? repainted = null;

        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromResult<TestModel?>(new TestModel("fresh")),
            onRefreshed: model =>
            {
                repainted = model;
                return Task.CompletedTask;
            }));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        Assert.Equal(new TestModel("fresh"), result.Model);
        Assert.Equal(new TestModel("fresh"), repainted);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<TestSnapshot>(s => s.Payload == "fresh")), Times.Once);
    }

    [Fact]
    public async Task RunAsync_FetchFails_KeepsPaintedSnapshot()
    {
        GivenStoredSnapshot("cached");
        var failure = new HttpRequestException("boom");

        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromException<TestModel?>(failure)));

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.Equal(new TestModel("cached"), result.Model);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        Assert.Same(failure, result.Error);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TestSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_FetchFailsWithoutSnapshot_ReportsBlockingFailure()
    {
        var failure = new HttpRequestException("boom");

        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromException<TestModel?>(failure)));

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.Null(result.Model);
        Assert.True(result.IsBlockingFailure);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public async Task RunAsync_FetchReturnsNothingWithoutSnapshot_ReportsEmpty()
    {
        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromResult<TestModel?>(null)));

        Assert.Equal(SnapshotRefreshOutcome.Empty, result.Outcome);
        Assert.Null(result.Model);
        Assert.False(result.IsBlockingFailure);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TestSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_UnreadableSnapshot_EvictsItAndLoadsFresh()
    {
        _snapshots.Setup(x => x.GetAsync<TestSnapshot>(_key)).ThrowsAsync(new InvalidOperationException("unreadable"));
        var missingReported = false;

        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromResult<TestModel?>(new TestModel("fresh")),
            onSnapshotMissing: () =>
            {
                missingReported = true;
                return Task.CompletedTask;
            }));

        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
        Assert.True(missingReported);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        Assert.Equal(new TestModel("fresh"), result.Model);
    }

    [Fact]
    public async Task RunAsync_RejectedSnapshot_LoadsFreshWithoutPainting()
    {
        GivenStoredSnapshot("belongs-to-another-surface");
        var request = Request(fetchAsync: () => Task.FromResult<TestModel?>(new TestModel("fresh")));

        var result = await CreateCoordinator().RunAsync(new SnapshotRefreshRequest<TestSnapshot, TestModel>
        {
            Key = request.Key,
            ToModel = _ => null,
            FetchAsync = request.FetchAsync,
            ToSnapshot = request.ToSnapshot,
        });

        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task RunAsync_SupersededRun_CommitsNeitherUiNorSnapshot()
    {
        var gate = new RefreshVersionGate();
        var fetchGate = new TaskCompletionSource<TestModel?>();
        var repainted = false;

        var run = CreateCoordinator().RunAsync(Request(
            fetchAsync: () => fetchGate.Task,
            onRefreshed: _ =>
            {
                repainted = true;
                return Task.CompletedTask;
            },
            gate: gate));

        // A newer load starts while the first fetch is still in flight.
        gate.Claim();
        fetchGate.SetResult(new TestModel("stale-result"));
        var result = await run;

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        Assert.False(repainted);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TestSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_CancelledSnapshotRead_DoesNotEvictSnapshot()
    {
        // Cancellation says nothing about whether the stored snapshot is usable, so it must survive.
        _snapshots.Setup(x => x.GetAsync<TestSnapshot>(_key)).ThrowsAsync(new OperationCanceledException());

        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromResult<TestModel?>(new TestModel("fresh"))));

        _snapshots.Verify(x => x.RemoveAsync(It.IsAny<string>()), Times.Never);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public async Task RunAsync_SnapshotWriteFails_KeepsFreshResult()
    {
        _snapshots.Setup(x => x.SetAsync(_key, It.IsAny<TestSnapshot>())).ThrowsAsync(new InvalidOperationException("storage full"));
        TestModel? repainted = null;

        var result = await CreateCoordinator().RunAsync(Request(
            fetchAsync: () => Task.FromResult<TestModel?>(new TestModel("fresh")),
            onRefreshed: model =>
            {
                repainted = model;
                return Task.CompletedTask;
            }));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        Assert.Equal(new TestModel("fresh"), result.Model);
        Assert.Equal(new TestModel("fresh"), repainted);
    }

    [Fact]
    public async Task RunAsync_CustomComparer_DecidesWhetherContentChanged()
    {
        GivenStoredSnapshot("CACHED");

        var result = await CreateCoordinator().RunAsync(new SnapshotRefreshRequest<TestSnapshot, TestModel>
        {
            Key = _key,
            ToModel = snapshot => new TestModel(snapshot.Payload),
            FetchAsync = () => Task.FromResult<TestModel?>(new TestModel("cached")),
            ToSnapshot = model => new TestSnapshot { Payload = model.Payload },
            ContentComparer = new CaseInsensitivePayloadComparer(),
        });

        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TestSnapshot>()), Times.Never);
    }

    private sealed class CaseInsensitivePayloadComparer : IEqualityComparer<TestModel>
    {
        public bool Equals(TestModel? x, TestModel? y) =>
            string.Equals(x?.Payload, y?.Payload, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(TestModel obj) => obj.Payload.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}