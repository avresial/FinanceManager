using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Shared.Services;

/// <summary>
/// One stale-while-revalidate run described declaratively: where the snapshot lives, how to turn
/// it into a rendered model, how to fetch fresh data, and how to turn the fresh model back into a
/// snapshot. Handed to <see cref="ISnapshotRefreshCoordinator.RunAsync"/>, which owns the ordering,
/// the equality check, the race protection and the non-fatal storage handling.
/// </summary>
/// <typeparam name="TSnapshot">The persisted snapshot type.</typeparam>
/// <typeparam name="TModel">The model the UI renders. Must not carry snapshot metadata.</typeparam>
public sealed class SnapshotRefreshRequest<TSnapshot, TModel>
    where TSnapshot : SnapshotBase
    where TModel : class
{
    /// <summary>
    /// Storage key for the snapshot. Scope it to the owning user and to the surface identity that
    /// changes what is rendered (currency, account), but not to the selected date range — one
    /// snapshot per surface is overwritten on each save.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Maps a stored snapshot to the model to paint. Return <c>null</c> to reject the snapshot as
    /// unusable (e.g. it belongs to a different account); the run then continues as a plain load.
    /// </summary>
    public required Func<TSnapshot, TModel?> ToModel { get; init; }

    /// <summary>
    /// Requests fresh data and maps it to the rendered model. Always invoked, snapshot or not.
    /// Return <c>null</c> when the response holds nothing to render; throw to report a failure.
    /// </summary>
    public required Func<Task<TModel?>> FetchAsync { get; init; }

    /// <summary>Maps the fresh model back to the snapshot to persist.</summary>
    public required Func<TModel, TSnapshot> ToSnapshot { get; init; }

    /// <summary>
    /// Decides whether fresh data actually changed what the user sees. Defaults to JSON equality
    /// over the model (see <see cref="JsonContentComparer{T}"/>).
    /// </summary>
    public IEqualityComparer<TModel> ContentComparer { get; init; } = JsonContentComparer<TModel>.Default;

    /// <summary>
    /// Race protection shared by every run against the same surface. When omitted the run always
    /// commits — only safe for surfaces that cannot be reloaded concurrently.
    /// </summary>
    public RefreshVersionGate? Gate { get; init; }

    /// <summary>
    /// A version already claimed from <see cref="Gate"/>. Set it when the caller has to claim before
    /// the run starts — typically because it resolves user/settings context first and needs the same
    /// version to guard that context's own state changes. When omitted the coordinator claims one.
    /// </summary>
    public int? ClaimedVersion { get; init; }

    /// <summary>Invoked with the snapshot model before the fresh request starts, so the surface paints immediately.</summary>
    public Func<TModel, Task>? OnSnapshotPainted { get; init; }

    /// <summary>Invoked instead of <see cref="OnSnapshotPainted"/> when no usable snapshot existed — the place to show a loading state.</summary>
    public Func<Task>? OnSnapshotMissing { get; init; }

    /// <summary>Invoked with the fresh model only when it differs from what was painted. Never invoked for an unchanged refresh.</summary>
    public Func<TModel, Task>? OnRefreshed { get; init; }
}