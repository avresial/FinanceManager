using Blazored.LocalStorage;
using FinanceManager.Components.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinanceManager.Components.Shared.Services;

/// <summary>
/// <see cref="ISnapshotService"/> backed by the browser's local storage. Snapshots
/// survive across navigations and page reloads for the same browser/user.
/// </summary>
public class LocalStorageSnapshotService(
    ILocalStorageService localStorageService,
    ILogger<LocalStorageSnapshotService> logger) : ISnapshotService
{
    private const string _keyPrefix = "ui-snapshot";

    public async Task<T?> GetAsync<T>(string key) where T : SnapshotBase
    {
        var storageKey = BuildStorageKey(key);
        try
        {
            return await localStorageService.GetItemAsync<T>(storageKey);
        }
        catch (JsonException ex)
        {
            // A snapshot whose shape no longer matches the current type is useless; drop it.
            logger.LogWarning(ex, "Corrupt {SnapshotType} snapshot payload; evicting.", typeof(T).Name);
            await localStorageService.RemoveItemAsync(storageKey);
            return null;
        }
    }

    public Task SetAsync<T>(string key, T snapshot) where T : SnapshotBase
        => localStorageService.SetItemAsync(BuildStorageKey(key), snapshot).AsTask();

    public Task RemoveAsync(string key)
        => localStorageService.RemoveItemAsync(BuildStorageKey(key)).AsTask();

    private static string BuildStorageKey(string key) => $"{_keyPrefix}:{key}";
}