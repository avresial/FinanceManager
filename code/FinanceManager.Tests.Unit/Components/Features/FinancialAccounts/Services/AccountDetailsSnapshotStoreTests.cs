using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Services;

[Trait("Category", "Unit")]
public class AccountDetailsSnapshotStoreTests
{
    private const string _key = "account-details:1:2";

    private readonly Mock<ISnapshotService> _snapshots = new();

    private AccountDetailsSnapshotStore CreateService() =>
        new(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));

    [Fact]
    public async Task RefreshAsync_ReadFailure_PaintsNothingAndLoadsFresh()
    {
        _snapshots.Setup(x => x.GetAsync<AccountDetailsSnapshot<int>>(_key)).ThrowsAsync(new InvalidOperationException());

        var result = await Refresh(fresh: Model([1, 2]));

        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_UnchangedEntries_SkipsWrite()
    {
        GivenStoredSnapshot([1, 2]);

        var result = await Refresh(fresh: Model([1, 2]));

        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AccountDetailsSnapshot<int>>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_ChangedEntries_WritesUserAndAccountScopedKey()
    {
        GivenStoredSnapshot([1, 2]);

        var result = await Refresh(fresh: Model([1, 3]));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<AccountDetailsSnapshot<int>>(
            s => s.UserId == 1 && s.AccountId == 2 && s.Entries.SequenceEqual(new[] { 1, 3 }))), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_SnapshotFromAnotherAccount_IsNotPainted()
    {
        // A stale entry left under this key by a different account must never be rendered here.
        _snapshots.Setup(x => x.GetAsync<AccountDetailsSnapshot<int>>(_key))
            .ReturnsAsync(new AccountDetailsSnapshot<int> { UserId = 1, AccountId = 99, Entries = [1, 2] });

        var painted = false;
        var result = await Refresh(fresh: Model([1, 2]), onSnapshotPainted: _ =>
        {
            painted = true;
            return Task.CompletedTask;
        });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    private void GivenStoredSnapshot(List<int> entries) =>
        _snapshots.Setup(x => x.GetAsync<AccountDetailsSnapshot<int>>(_key))
            .ReturnsAsync(new AccountDetailsSnapshot<int> { UserId = 1, AccountId = 2, AccountType = AccountLabel.Other, Entries = entries });

    private Task<SnapshotRefreshResult<AccountDetailsModel<int>>> Refresh(
        AccountDetailsModel<int>? fresh,
        Func<AccountDetailsModel<int>, Task>? onSnapshotPainted = null) =>
        CreateService().RefreshAsync(1, 2, new RefreshVersionGate(), () => Task.FromResult(fresh), onSnapshotPainted);

    private static AccountDetailsModel<int> Model(List<int> entries) => new(1, 2, string.Empty, AccountLabel.Other, entries);
}