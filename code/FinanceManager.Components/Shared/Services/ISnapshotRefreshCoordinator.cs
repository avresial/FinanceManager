using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Shared.Services;

/// <summary>
/// Runs the stale-while-revalidate UI snapshot workflow: paint the last-rendered state from local
/// storage, always re-request fresh data, and reconcile the two.
/// </summary>
/// <remarks>
/// <para>This is not a data cache. The fresh request always runs; the snapshot only decides what is
/// on screen while it is in flight. Use <c>LocalStorageStateCacheService</c> instead when the goal
/// is to <em>skip</em> the API request while cached data is still valid.</para>
/// <para>Per run the coordinator guarantees:</para>
/// <list type="bullet">
/// <item>a snapshot read failure is logged, the unusable entry is evicted, and the run continues as a plain load;</item>
/// <item>the fresh request runs whether or not a snapshot was painted;</item>
/// <item>fresh data equal to the painted snapshot repaints nothing and writes nothing;</item>
/// <item>a failed fresh request leaves a painted snapshot on screen, and is only blocking when nothing was painted;</item>
/// <item>a run superseded on the version gate commits neither UI nor storage;</item>
/// <item>a snapshot write failure is logged but never discards the fresh result.</item>
/// </list>
/// </remarks>
public interface ISnapshotRefreshCoordinator
{
    /// <summary>Executes <paramref name="request"/> and reports what happened.</summary>
    Task<SnapshotRefreshResult<TModel>> RunAsync<TSnapshot, TModel>(SnapshotRefreshRequest<TSnapshot, TModel> request)
        where TSnapshot : SnapshotBase
        where TModel : class;
}