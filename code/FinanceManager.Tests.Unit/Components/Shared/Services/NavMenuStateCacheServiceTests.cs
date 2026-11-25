using Blazored.LocalStorage;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Identity.Entities;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Shared.Services;

[Trait("Category", "Unit")]
public class NavMenuStateCacheServiceTests
{
    private static UserSession User(int id) => new() { UserId = id, UserName = "user", Password = string.Empty, UserRole = UserRole.User };

    [Fact]
    public async Task GetCachedSnapshotAsync_EvictsASnapshotFromAnOlderSchema()
    {
        // The snapshot shape changed (accounts removed, HasTransactions added), so one cached by an older schema must not paint.
        var backend = new NavMenuTestBackend();
        backend.Storage.Setup(x => x.GetItemAsync<NavMenuCacheSnapshot>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<NavMenuCacheSnapshot?>(new NavMenuCacheSnapshot
            {
                SchemaVersion = NavMenuCacheSnapshot.CurrentSchemaVersion - 1,
                UserId = 5,
                FetchedAtUtc = DateTime.UtcNow,
                DisplayLiabilitiesLink = true,
            }));

        Assert.Null(await backend.CreateService().GetCachedSnapshotAsync(5));
        backend.Storage.Verify(x => x.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCachedSnapshotAsync_ReturnsASnapshotFromTheCurrentSchema()
    {
        var backend = new NavMenuTestBackend();
        backend.Storage.Setup(x => x.GetItemAsync<NavMenuCacheSnapshot>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<NavMenuCacheSnapshot?>(new NavMenuCacheSnapshot { UserId = 5, FetchedAtUtc = DateTime.UtcNow }));

        Assert.NotNull(await backend.CreateService().GetCachedSnapshotAsync(5));
    }

    [Fact]
    public async Task GetCachedSnapshotAsync_ReadsTheV2KeyOnly_SoAV1SnapshotIsNeverUsed()
    {
        var backend = new NavMenuTestBackend();

        await backend.CreateService().GetCachedSnapshotAsync(5);

        backend.Storage.Verify(x => x.GetItemAsync<NavMenuCacheSnapshot>("nav-menu-cache-v2:5", It.IsAny<CancellationToken>()), Times.Once);
        backend.Storage.Verify(x => x.GetItemAsync<NavMenuCacheSnapshot>("nav-menu-cache-v1:5", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task RefreshAsync_ComputesHasTransactionsFromTheExistenceProbe(int transactionCount, bool expected)
    {
        var backend = new NavMenuTestBackend(hasAssets: true, hasLiabilities: true, transactionCount: transactionCount);

        var snapshot = await backend.CreateService().RefreshAsync(User(5));

        Assert.Equal(expected, snapshot.HasTransactions);
        Assert.True(snapshot.DisplayAssetsLink);
        Assert.True(snapshot.DisplayLiabilitiesLink);
        Assert.Equal("api/TransactionLog/Exists/5", Assert.Single(backend.Requests, path => path.StartsWith("api/TransactionLog/", StringComparison.Ordinal)));
        backend.Storage.Verify(x => x.SetItemAsync("nav-menu-cache-v2:5", snapshot, It.IsAny<CancellationToken>()), Times.Once);
    }
}