using Blazored.LocalStorage;
using FinanceManager.Components.Shared.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Shared.Services;

[Trait("Category", "Unit")]
public class LocalStorageStateCacheServiceTests
{
    private const string _prefix = "test-cache-v1";
    private const string _currentKey = "7:0:2026-07-29";
    private const string _currentStorageKey = $"{_prefix}:{_currentKey}";
    private const string _supersededStorageKey = $"{_prefix}:7:0:2026-07-28";
    private const string _foreignStorageKey = "other-cache-v1:7:0:2026-07-28";

    private sealed class TestState
    {
        public string CacheKey { get; set; } = string.Empty;
    }

    private sealed class TestStateCache(
        ILocalStorageService localStorageService,
        IMemoryCache memoryCache,
        bool retainsOnlyLatestEntry)
        : LocalStorageStateCacheService<TestState, string, string>(
            localStorageService,
            memoryCache,
            NullLogger.Instance,
            _prefix)
    {
        protected override bool RetainsOnlyLatestEntry => retainsOnlyLatestEntry;

        protected override string GetCacheKey(string refreshContext) => refreshContext;

        protected override Task<TestState> BuildStateAsync(string refreshContext) =>
            Task.FromResult(new TestState { CacheKey = refreshContext });

        protected override bool IsUsable(TestState? state, string cacheKey, DateTime utcNow) =>
            state is not null && state.CacheKey == cacheKey;
    }

    private static TestStateCache CreateCache(Mock<ILocalStorageService> localStorage, bool retainsOnlyLatestEntry) =>
        new(localStorage.Object, new MemoryCache(new MemoryCacheOptions()), retainsOnlyLatestEntry);

    private static Mock<ILocalStorageService> CreateLocalStorage(params string[] existingKeys)
    {
        var localStorage = new Mock<ILocalStorageService>();
        localStorage
            .Setup(s => s.KeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingKeys);
        return localStorage;
    }

    [Fact]
    public async Task Write_WhenRetainingOnlyLatestEntry_RemovesEntriesOfItsOwnPrefixOnly()
    {
        var localStorage = CreateLocalStorage(_supersededStorageKey, _currentStorageKey, _foreignStorageKey);
        var cache = CreateCache(localStorage, retainsOnlyLatestEntry: true);

        await cache.GetOrRefreshAsync(_currentKey);

        localStorage.Verify(
            s => s.RemoveItemsAsync(
                It.Is<IEnumerable<string>>(keys => keys.SequenceEqual(new[] { _supersededStorageKey })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Write_WhenNotRetainingOnlyLatestEntry_KeepsSiblingEntries()
    {
        var localStorage = CreateLocalStorage(_supersededStorageKey, _currentStorageKey);
        var cache = CreateCache(localStorage, retainsOnlyLatestEntry: false);

        await cache.GetOrRefreshAsync(_currentKey);

        localStorage.Verify(
            s => s.RemoveItemsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Write_WhenNothingToPrune_DoesNotTouchStorage()
    {
        var localStorage = CreateLocalStorage(_currentStorageKey);
        var cache = CreateCache(localStorage, retainsOnlyLatestEntry: true);

        await cache.GetOrRefreshAsync(_currentKey);

        localStorage.Verify(
            s => s.RemoveItemsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Write_WhenPruningFails_StillReturnsTheFreshState()
    {
        var localStorage = new Mock<ILocalStorageService>();
        localStorage
            .Setup(s => s.KeysAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("local storage unavailable"));
        var cache = CreateCache(localStorage, retainsOnlyLatestEntry: true);

        var state = await cache.GetOrRefreshAsync(_currentKey);

        Assert.Equal(_currentKey, state.CacheKey);
        localStorage.Verify(
            s => s.SetItemAsync(_currentStorageKey, It.IsAny<TestState>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_PrunesSupersededEntriesToo()
    {
        var localStorage = CreateLocalStorage(_supersededStorageKey, _currentStorageKey);
        var cache = CreateCache(localStorage, retainsOnlyLatestEntry: true);

        await cache.RefreshAsync(_currentKey);

        localStorage.Verify(
            s => s.RemoveItemsAsync(
                It.Is<IEnumerable<string>>(keys => keys.SequenceEqual(new[] { _supersededStorageKey })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}