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
    public async Task TotalTrackedMoney_PaintsBeforeFetch_AlwaysFetches_AndSkipsUnchangedWrite()
    {
        const string key = "admin-total-tracked-money-pln:1";
        _snapshots.Setup(x => x.GetAsync<AdminTotalTrackedMoneySnapshot>(key))
            .ReturnsAsync(new AdminTotalTrackedMoneySnapshot { UserId = 1, Amount = 5, FetchedAtUtc = DateTime.UtcNow.AddYears(-1) });
        var painted = false;
        var fetchedAfterPaint = false;

        var result = await CreateStore().RefreshTotalTrackedMoneyAsync(
            1,
            new RefreshVersionGate(),
            () =>
            {
                fetchedAfterPaint = painted;
                return Task.FromResult<decimal?>(5);
            },
            model =>
            {
                painted = model.Amount == 5;
                return Task.CompletedTask;
            });

        Assert.True(fetchedAfterPaint);
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminTotalTrackedMoneySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task TotalTrackedMoney_Changed_WritesUserScopedSnapshot()
    {
        const string key = "admin-total-tracked-money-pln:1";

        var result = await CreateStore().RefreshTotalTrackedMoneyAsync(
            1,
            new RefreshVersionGate(),
            () => Task.FromResult<decimal?>(7));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminTotalTrackedMoneySnapshot>(s => s.UserId == 1 && s.Amount == 7)), Times.Once);
    }

    [Fact]
    public async Task TotalTrackedMoney_RejectsAnotherUsersSnapshot()
    {
        const string key = "admin-total-tracked-money-pln:1";
        _snapshots.Setup(x => x.GetAsync<AdminTotalTrackedMoneySnapshot>(key))
            .ReturnsAsync(new AdminTotalTrackedMoneySnapshot { UserId = 2, Amount = 9 });
        var painted = false;

        var result = await CreateStore().RefreshTotalTrackedMoneyAsync(
            1,
            new RefreshVersionGate(),
            () => Task.FromResult<decimal?>(3),
            _ =>
            {
                painted = true;
                return Task.CompletedTask;
            });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminTotalTrackedMoneySnapshot>(s => s.UserId == 1 && s.Amount == 3)), Times.Once);
    }

    [Fact]
    public async Task TotalTrackedMoney_FailedRefresh_KeepsPaintedModel()
    {
        const string key = "admin-total-tracked-money-pln:1";
        _snapshots.Setup(x => x.GetAsync<AdminTotalTrackedMoneySnapshot>(key))
            .ReturnsAsync(new AdminTotalTrackedMoneySnapshot { UserId = 1, Amount = 5, FetchedAtUtc = DateTime.UtcNow.AddYears(-1) });

        var result = await CreateStore().RefreshTotalTrackedMoneyAsync(
            1,
            new RefreshVersionGate(),
            () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        Assert.Equal(5, result.Model!.Amount);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminTotalTrackedMoneySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task TotalTrackedMoney_NoUsableResponseWithoutSnapshot_LeavesCardLoading()
    {
        var result = await CreateStore().RefreshTotalTrackedMoneyAsync(
            1,
            new RefreshVersionGate(),
            () => Task.FromResult<decimal?>(null));

        Assert.Equal(SnapshotRefreshOutcome.Empty, result.Outcome);
        Assert.Null(result.Model);
        Assert.False(result.IsBlockingFailure);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminTotalTrackedMoneySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task TotalTrackedMoney_GenuinelyZero_RefreshesToZero()
    {
        const string key = "admin-total-tracked-money-pln:1";
        _snapshots.Setup(x => x.GetAsync<AdminTotalTrackedMoneySnapshot>(key))
            .ReturnsAsync(new AdminTotalTrackedMoneySnapshot { UserId = 1, Amount = 5, FetchedAtUtc = DateTime.UtcNow.AddYears(-1) });

        var result = await CreateStore().RefreshTotalTrackedMoneyAsync(
            1,
            new RefreshVersionGate(),
            () => Task.FromResult<decimal?>(0));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        Assert.Equal(0, result.Model!.Amount);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminTotalTrackedMoneySnapshot>(s => s.Amount == 0)), Times.Once);
    }

    [Fact]
    public async Task TotalTrackedMoney_SupersededRequest_DoesNotWrite()
    {
        var gate = new RefreshVersionGate();
        var claimed = gate.Claim();

        var result = await CreateStore().RefreshTotalTrackedMoneyAsync(
            1,
            gate,
            () =>
            {
                gate.Claim();
                return Task.FromResult<decimal?>(4);
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminTotalTrackedMoneySnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData("1", "1.000")]
    [InlineData("1.254", "1.251")]
    public async Task TotalTrackedMoney_SameDisplayedCents_DoesNotRepaintOrWrite(string stored, string fetched)
    {
        _snapshots.Setup(x => x.GetAsync<AdminTotalTrackedMoneySnapshot>("admin-total-tracked-money-pln:1"))
            .ReturnsAsync(new AdminTotalTrackedMoneySnapshot { UserId = 1, Amount = decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture) });
        var repainted = false;
        var result = await CreateStore().RefreshTotalTrackedMoneyAsync(1, new RefreshVersionGate(),
            () => Task.FromResult<decimal?>(decimal.Parse(fetched, System.Globalization.CultureInfo.InvariantCulture)),
            onRefreshed: _ => { repainted = true; return Task.CompletedTask; });
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        Assert.False(repainted);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminTotalTrackedMoneySnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TotalTrackedMoney_StorageFailures_DoNotDiscardFreshAmount(bool readFails)
    {
        const string key = "admin-total-tracked-money-pln:1";
        if (readFails)
            _snapshots.Setup(x => x.GetAsync<AdminTotalTrackedMoneySnapshot>(key)).ThrowsAsync(new InvalidOperationException("Storage unavailable"));
        else
            _snapshots.Setup(x => x.SetAsync(key, It.IsAny<AdminTotalTrackedMoneySnapshot>())).ThrowsAsync(new InvalidOperationException("Storage unavailable"));
        decimal? rendered = null;
        var result = await CreateStore().RefreshTotalTrackedMoneyAsync(1, new RefreshVersionGate(), () => Task.FromResult<decimal?>(1.25m),
            onRefreshed: model => { rendered = model.Amount; return Task.CompletedTask; });
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        Assert.Equal(1.25m, rendered);
    }

    [Fact]
    public async Task UsersCount_PaintsBeforeFetch_AlwaysFetches_AndSkipsUnchangedWrite()
    {
        const string key = "admin-users-count:1";
        _snapshots.Setup(x => x.GetAsync<AdminUsersCountSnapshot>(key))
            .ReturnsAsync(new AdminUsersCountSnapshot { UserId = 1, Count = 5 });
        var painted = false;
        var fetchedAfterPaint = false;

        var result = await CreateStore().RefreshUsersCountAsync(
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
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminUsersCountSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task UsersCount_Changed_WritesUserScopedSnapshot()
    {
        const string key = "admin-users-count:1";

        var result = await CreateStore().RefreshUsersCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(7));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminUsersCountSnapshot>(s => s.UserId == 1 && s.Count == 7)), Times.Once);
    }

    [Fact]
    public async Task UsersCount_RejectsAnotherUsersSnapshot()
    {
        const string key = "admin-users-count:1";
        _snapshots.Setup(x => x.GetAsync<AdminUsersCountSnapshot>(key))
            .ReturnsAsync(new AdminUsersCountSnapshot { UserId = 2, Count = 9 });
        var painted = false;

        var result = await CreateStore().RefreshUsersCountAsync(
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
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminUsersCountSnapshot>(s => s.UserId == 1 && s.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task UsersCount_FailedRefresh_KeepsPaintedModel()
    {
        const string key = "admin-users-count:1";
        _snapshots.Setup(x => x.GetAsync<AdminUsersCountSnapshot>(key))
            .ReturnsAsync(new AdminUsersCountSnapshot { UserId = 1, Count = 5 });

        var result = await CreateStore().RefreshUsersCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        Assert.Equal(5, result.Model!.Count);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminUsersCountSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task UsersCount_NoUsableResponseWithoutSnapshot_LeavesCardLoading()
    {
        var result = await CreateStore().RefreshUsersCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(null));

        Assert.Equal(SnapshotRefreshOutcome.Empty, result.Outcome);
        Assert.Null(result.Model);
        Assert.False(result.IsBlockingFailure);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminUsersCountSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task UsersCount_GenuinelyZero_RefreshesToZero()
    {
        const string key = "admin-users-count:1";
        _snapshots.Setup(x => x.GetAsync<AdminUsersCountSnapshot>(key))
            .ReturnsAsync(new AdminUsersCountSnapshot { UserId = 1, Count = 5 });

        var result = await CreateStore().RefreshUsersCountAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<int?>(0));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        Assert.Equal(0, result.Model!.Count);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminUsersCountSnapshot>(s => s.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task UsersCount_SupersededRequest_DoesNotWrite()
    {
        var gate = new RefreshVersionGate();
        var claimed = gate.Claim();

        var result = await CreateStore().RefreshUsersCountAsync(
            1,
            gate,
            claimed,
            () =>
            {
                gate.Claim();
                return Task.FromResult<int?>(4);
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminUsersCountSnapshot>()), Times.Never);
    }

    private static readonly DateTime _today = new(2026, 10, 2);

    [Fact]
    public async Task NewVisitorsToday_PreviousDaysSnapshot_IsNotPaintedEvenWhenRefreshFails()
    {
        const string key = "admin-new-visitors-today:1";
        _snapshots.Setup(x => x.GetAsync<AdminNewVisitorsTodaySnapshot>(key))
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 1, Day = _today.AddDays(-1), Count = 5 });
        var painted = false;

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            _today,
            new RefreshVersionGate(),
            null,
            _ => throw new HttpRequestException(),
            _ =>
            {
                painted = true;
                return Task.CompletedTask;
            });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Null(result.Model);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminNewVisitorsTodaySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task NewVisitorsToday_FetchesAndStoresTheCapturedDay()
    {
        DateTime? fetchedDay = null;

        await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            _today.AddHours(23).AddMinutes(59),
            new RefreshVersionGate(),
            null,
            day =>
            {
                fetchedDay = day;
                return Task.FromResult<int?>(4);
            });

        Assert.Equal(_today, fetchedDay);
        _snapshots.Verify(x => x.SetAsync("admin-new-visitors-today:1", It.Is<AdminNewVisitorsTodaySnapshot>(s => s.Day == _today && s.Count == 4)), Times.Once);
    }

    [Fact]
    public async Task NewVisitorsToday_PaintsBeforeFetch_AlwaysFetches_AndSkipsUnchangedWrite()
    {
        const string key = "admin-new-visitors-today:1";
        _snapshots.Setup(x => x.GetAsync<AdminNewVisitorsTodaySnapshot>(key))
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 1, Day = _today, Count = 5 });
        var painted = false;
        var fetchedAfterPaint = false;

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            _today,
            new RefreshVersionGate(),
            null,
            _ =>
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
            _today,
            new RefreshVersionGate(),
            null,
            _ => Task.FromResult<int?>(7));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<AdminNewVisitorsTodaySnapshot>(s => s.UserId == 1 && s.Day == _today && s.Count == 7)), Times.Once);
    }

    [Fact]
    public async Task NewVisitorsToday_RejectsAnotherUsersSnapshot()
    {
        const string key = "admin-new-visitors-today:1";
        _snapshots.Setup(x => x.GetAsync<AdminNewVisitorsTodaySnapshot>(key))
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 2, Day = _today, Count = 9 });
        var painted = false;

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            _today,
            new RefreshVersionGate(),
            null,
            _ => Task.FromResult<int?>(3),
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
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 1, Day = _today, Count = 5 });

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            _today,
            new RefreshVersionGate(),
            null,
            _ => throw new HttpRequestException());

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
            _today,
            new RefreshVersionGate(),
            null,
            _ => Task.FromResult<int?>(null));

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
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 1, Day = _today, Count = 5 });

        var result = await CreateStore().RefreshNewVisitorsTodayAsync(
            1,
            _today,
            new RefreshVersionGate(),
            null,
            _ => Task.FromResult<int?>(0));

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
            _today,
            gate,
            claimed,
            _ =>
            {
                gate.Claim();
                return Task.FromResult<int?>(4);
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AdminNewVisitorsTodaySnapshot>()), Times.Never);
    }
}