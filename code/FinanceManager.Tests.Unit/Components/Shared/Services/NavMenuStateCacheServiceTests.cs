using Blazored.LocalStorage;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Shared.Services;

[Trait("Category", "Unit")]
public class NavMenuStateCacheServiceTests
{
    private readonly Mock<ILocalStorageService> _storage = new();
    private readonly NavMenuStateCacheService _service;

    public NavMenuStateCacheServiceTests() => _service = new(
        _storage.Object, new MemoryCache(new MemoryCacheOptions()), Mock.Of<IFinancialAccountService>(),
        new AssetsHttpClient(new HttpClient()), new LiabilitiesHttpClient(new HttpClient()), NullLogger<NavMenuStateCacheService>.Instance);

    [Fact]
    public async Task GetCachedSnapshotAsync_EvictsASnapshotFromAnOlderSchema()
    {
        // Pooling changed what the Assets and Liabilities links mean, so a flag cached by the previous schema must not paint.
        _storage.Setup(x => x.GetItemAsync<NavMenuCacheSnapshot>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<NavMenuCacheSnapshot?>(new NavMenuCacheSnapshot
            {
                SchemaVersion = NavMenuCacheSnapshot.CurrentSchemaVersion - 1,
                UserId = 5,
                FetchedAtUtc = DateTime.UtcNow,
                DisplayLiabilitiesLink = true,
            }));

        Assert.Null(await _service.GetCachedSnapshotAsync(5));
        _storage.Verify(x => x.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCachedSnapshotAsync_ReturnsASnapshotFromTheCurrentSchema()
    {
        _storage.Setup(x => x.GetItemAsync<NavMenuCacheSnapshot>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<NavMenuCacheSnapshot?>(new NavMenuCacheSnapshot { UserId = 5, FetchedAtUtc = DateTime.UtcNow }));

        Assert.NotNull(await _service.GetCachedSnapshotAsync(5));
    }
}