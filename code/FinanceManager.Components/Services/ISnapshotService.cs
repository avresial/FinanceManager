using FinanceManager.Components.Models;

namespace FinanceManager.Components.Services;

/// <summary>
/// Cache-like store for UI snapshots. Mirrors a cache surface: <see cref="SetAsync"/>
/// and <see cref="RemoveAsync"/> write, while <see cref="GetAsync"/> reads the snapshot
/// back so a component can paint its last-rendered state before fresh data arrives.
/// </summary>
/// <remarks>
/// Keys should be scoped to the owning user (e.g. include the user id) and must not
/// encode a date/range, so a single snapshot per surface is overwritten on each save.
/// </remarks>
public interface ISnapshotService
{
    /// <summary>Reads the snapshot stored under <paramref name="key"/>, or <c>null</c> when none exists or it is unreadable.</summary>
    Task<T?> GetAsync<T>(string key) where T : SnapshotBase;

    /// <summary>Persists <paramref name="snapshot"/> under <paramref name="key"/>, overwriting any existing value.</summary>
    Task SetAsync<T>(string key, T snapshot) where T : SnapshotBase;

    /// <summary>Removes the snapshot stored under <paramref name="key"/>.</summary>
    Task RemoveAsync(string key);
}