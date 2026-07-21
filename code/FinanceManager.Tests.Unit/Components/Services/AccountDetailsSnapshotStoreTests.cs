using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Services;

[Trait("Category", "Unit")]
public class AccountDetailsSnapshotStoreTests
{
    private readonly Mock<ISnapshotService> _snapshots = new();

    private AccountDetailsSnapshotStore CreateService() =>
        new(_snapshots.Object, NullLogger<AccountDetailsSnapshotStore>.Instance);

    [Fact]
    public async Task GetAsync_ReadFailure_ReturnsNull()
    {
        _snapshots.Setup(x => x.GetAsync<AccountDetailsSnapshot<int>>("account-details:1:2"))
            .ThrowsAsync(new InvalidOperationException());

        Assert.Null(await CreateService().GetAsync<int>(1, 2));
    }

    [Fact]
    public async Task SaveIfChangedAsync_UnchangedEntries_SkipsWrite()
    {
        var previous = Snapshot([1, 2]);

        await CreateService().SaveIfChangedAsync(Snapshot([1, 2]), previous);

        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<AccountDetailsSnapshot<int>>()), Times.Never);
    }

    [Fact]
    public async Task SaveIfChangedAsync_ChangedEntries_WritesUserAndAccountScopedKey()
    {
        var snapshot = Snapshot([1, 3]);

        await CreateService().SaveIfChangedAsync(snapshot, Snapshot([1, 2]));

        _snapshots.Verify(x => x.SetAsync("account-details:1:2", snapshot), Times.Once);
    }

    private static AccountDetailsSnapshot<int> Snapshot(List<int> entries) => new()
    {
        UserId = 1,
        AccountId = 2,
        Entries = entries,
    };
}