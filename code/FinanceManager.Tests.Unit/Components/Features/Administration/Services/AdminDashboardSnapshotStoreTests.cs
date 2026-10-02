using FinanceManager.Components.Features.Administration.Models;
using FinanceManager.Components.Features.Administration.Services;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.Administration.Services;

[Trait("Category", "Unit")]
public class AdminDashboardSnapshotStoreTests
{
    private readonly Mock<ISnapshotService> _snapshots = new();

    private AdminDashboardSnapshotStore CreateStore() =>
        new(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));

    [Fact]
    public async Task AccountsCount_PaintsBeforeFetch_AlwaysFetches_AndSkipsUnchangedWrite()
    {
        const string key = "admin-accounts-count:1";
        _snapshots.Setup(x => x.GetAsync<AdminAccountsCountSnapshot>(key))
            .ReturnsAsync(new AdminAccountsCountSnapshot { UserId = 1, Count = 5 });
        var painted = false;
        var fetchedAfterPaint = false;

        var result = await CreateStore().RefreshAccountsCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () =>
            {
                fetchedAfterPaint = painted;
                return Task.FromResult<int?>(5);
            },
            model =>
            {
                painted = model.Count == 5;
                return Task.CompletedTask;
            });

        Assert.True(fetchedAfterPaint);
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminAccountsCountSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task AccountsCount_Changed_WritesUserScopedSnapshot()
    {
        const string key = "admin-accounts-count:1";

        var result = await CreateStore().RefreshAccountsCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(7));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminAccountsCountSnapshot>(s => s.UserId == 1 && s.Count == 7)), Times.Once);
    }

    [Fact]
    public async Task AccountsCount_RejectsAnotherUsersSnapshot()
    {
        const string key = "admin-accounts-count:1";
        _snapshots.Setup(x => x.GetAsync<AdminAccountsCountSnapshot>(key))
            .ReturnsAsync(new AdminAccountsCountSnapshot { UserId = 2, Count = 9 });
        var painted = false;

        var result = await CreateStore().RefreshAccountsCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(3),
            _ =>
            {
                painted = true;
                return Task.CompletedTask;
            });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminAccountsCountSnapshot>(s => s.UserId == 1 && s.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task AccountsCount_FailedRefresh_KeepsPaintedModel()
    {
        const string key = "admin-accounts-count:1";
        _snapshots.Setup(x => x.GetAsync<AdminAccountsCountSnapshot>(key))
            .ReturnsAsync(new AdminAccountsCountSnapshot { UserId = 1, Count = 5 });

        var result = await CreateStore().RefreshAccountsCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        Assert.Equal(5, result.Model!.Count);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminAccountsCountSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task AccountsCount_NoUsableResponseWithoutSnapshot_LeavesCardLoading()
    {
        var result = await CreateStore().RefreshAccountsCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(null));

        Assert.Equal(SnapshotRefreshOutcome.Empty, result.Outcome);
        Assert.Null(result.Model);
        Assert.False(result.IsBlockingFailure);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminAccountsCountSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task AccountsCount_GenuinelyZero_RefreshesToZero()
    {
        const string key = "admin-accounts-count:1";
        _snapshots.Setup(x => x.GetAsync<AdminAccountsCountSnapshot>(key))
            .ReturnsAsync(new AdminAccountsCountSnapshot { UserId = 1, Count = 5 });

        var result = await CreateStore().RefreshAccountsCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(0));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        Assert.Equal(0, result.Model!.Count);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminAccountsCountSnapshot>(s => s.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task AccountsCount_SupersededRequest_DoesNotWrite()
    {
        var gate = new RefreshVersionGate();
        var claimed = gate.Claim();

        var result = await CreateStore().RefreshAccountsCountAsync(
            1,
            gate,
            claimed,
            () =>
            {
                gate.Claim();
                return Task.FromResult<int?>(4);
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminAccountsCountSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task NewVisitorsToday_PaintsBeforeFetch_AlwaysFetches_AndSkipsUnchangedWrite()
    {
        const string key = "admin-new-visitors-today:1";
        _snapshots.Setup(x => x.GetAsync<AdminNewVisitorsTodaySnapshot>(key))
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 1, Count = 5 });
        var painted = false;
        var fetchedAfterPaint = false;

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            new RefreshVersionGate(),
            null,
            () =>
            {
                fetchedAfterPaint = painted;
                return Task.FromResult<int?>(5);
            },
            model =>
            {
                painted = model.Count == 5;
                return Task.CompletedTask;
            });

        Assert.True(fetchedAfterPaint);
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminNewVisitorsTodaySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task NewVisitorsToday_Changed_WritesUserScopedSnapshot()
    {
        const string key = "admin-new-visitors-today:1";

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(7));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminNewVisitorsTodaySnapshot>(s => s.UserId == 1 && s.Count == 7)), Times.Once);
    }

    [Fact]
    public async Task NewVisitorsToday_RejectsAnotherUsersSnapshot()
    {
        const string key = "admin-new-visitors-today:1";
        _snapshots.Setup(x => x.GetAsync<AdminNewVisitorsTodaySnapshot>(key))
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 2, Count = 9 });
        var painted = false;

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(3),
            _ =>
            {
                painted = true;
                return Task.CompletedTask;
            });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminNewVisitorsTodaySnapshot>(s => s.UserId == 1 && s.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task NewVisitorsToday_FailedRefresh_KeepsPaintedModel()
    {
        const string key = "admin-new-visitors-today:1";
        _snapshots.Setup(x => x.GetAsync<AdminNewVisitorsTodaySnapshot>(key))
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 1, Count = 5 });

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        Assert.Equal(5, result.Model!.Count);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminNewVisitorsTodaySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task NewVisitorsToday_NoUsableResponseWithoutSnapshot_LeavesCardLoading()
    {
        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(null));

        Assert.Equal(SnapshotRefreshOutcome.Empty, result.Outcome);
        Assert.Null(result.Model);
        Assert.False(result.IsBlockingFailure);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminNewVisitorsTodaySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task NewVisitorsToday_GenuinelyZero_RefreshesToZero()
    {
        const string key = "admin-new-visitors-today:1";
        _snapshots.Setup(x => x.GetAsync<AdminNewVisitorsTodaySnapshot>(key))
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 1, Count = 5 });

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(0));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        Assert.Equal(0, result.Model!.Count);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminNewVisitorsTodaySnapshot>(s => s.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task NewVisitorsToday_SupersededRequest_DoesNotWrite()
    {
        var gate = new RefreshVersionGate();
        var claimed = gate.Claim();

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            gate,
            claimed,
            () =>
            {
                gate.Claim();
                return Task.FromResult<int?>(4);
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminNewVisitorsTodaySnapshot>()), Times.Never);
    }
}