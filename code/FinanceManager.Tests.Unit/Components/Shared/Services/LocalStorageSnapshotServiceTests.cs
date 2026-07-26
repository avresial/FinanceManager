using Blazored.LocalStorage;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Shared.Services;

[Trait("Category", "Unit")]
public class LocalStorageSnapshotServiceTests
{
    private const string _key = "dashboard-overview:7";
    private const string _storageKey = "ui-snapshot:dashboard-overview:7";

    private sealed class TestSnapshot : SnapshotBase
    {
        public string Payload { get; set; } = string.Empty;
    }

    [Fact]
    public async Task GetAsync_PrefixesKey_AndReturnsStoredSnapshot()
    {
        var stored = new TestSnapshot { Payload = "hello" };
        var localStorage = new Mock<ILocalStorageService>();
        localStorage
            .Setup(s => s.GetItemAsync<TestSnapshot>(_storageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var service = new LocalStorageSnapshotService(localStorage.Object, NullLogger<LocalStorageSnapshotService>.Instance);

        var result = await service.GetAsync<TestSnapshot>(_key);

        Assert.Same(stored, result);
    }

    [Fact]
    public async Task GetAsync_OnCorruptPayload_EvictsAndReturnsNull()
    {
        var localStorage = new Mock<ILocalStorageService>();
        localStorage
            .Setup(s => s.GetItemAsync<TestSnapshot>(_storageKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("corrupt"));

        var service = new LocalStorageSnapshotService(localStorage.Object, NullLogger<LocalStorageSnapshotService>.Instance);

        var result = await service.GetAsync<TestSnapshot>(_key);

        Assert.Null(result);
        localStorage.Verify(s => s.RemoveItemAsync(_storageKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_PersistsUnderPrefixedKey()
    {
        var snapshot = new TestSnapshot { Payload = "world" };
        var localStorage = new Mock<ILocalStorageService>();

        var service = new LocalStorageSnapshotService(localStorage.Object, NullLogger<LocalStorageSnapshotService>.Instance);

        await service.SetAsync(_key, snapshot);

        localStorage.Verify(s => s.SetItemAsync(_storageKey, snapshot, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_RemovesPrefixedKey()
    {
        var localStorage = new Mock<ILocalStorageService>();

        var service = new LocalStorageSnapshotService(localStorage.Object, NullLogger<LocalStorageSnapshotService>.Instance);

        await service.RemoveAsync(_key);

        localStorage.Verify(s => s.RemoveItemAsync(_storageKey, It.IsAny<CancellationToken>()), Times.Once);
    }
}