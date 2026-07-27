using FinanceManager.Components.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Shared.Services;

/// <summary>
/// <see cref="ISnapshotRefreshCoordinator"/> over <see cref="ISnapshotService"/>. Holds no state of
/// its own — race protection lives in the <see cref="RefreshVersionGate"/> the caller owns — so a
/// single instance can serve every surface.
/// </summary>
public sealed class SnapshotRefreshCoordinator(
    ISnapshotService snapshots,
    ILogger<SnapshotRefreshCoordinator> logger) : ISnapshotRefreshCoordinator
{
    public async Task<SnapshotRefreshResult<TModel>> RunAsync<TSnapshot, TModel>(SnapshotRefreshRequest<TSnapshot, TModel> request)
        where TSnapshot : SnapshotBase
        where TModel : class
    {
        var version = request.ClaimedVersion ?? request.Gate?.Claim() ?? 0;
        bool IsCurrent() => request.Gate is null || request.Gate.IsCurrent(version);

        var snapshot = await ReadSnapshotAsync(request.Key, typeof(TSnapshot).Name, () => snapshots.GetAsync<TSnapshot>(request.Key));

        if (!IsCurrent())
            return new SnapshotRefreshResult<TModel>(SnapshotRefreshOutcome.Superseded, null, false);

        // Paint the last-rendered state first so the surface feels instant; the fetch below runs regardless.
        var paintedModel = snapshot is null ? null : request.ToModel(snapshot);
        if (paintedModel is not null)
        {
            if (request.OnSnapshotPainted is not null)
                await request.OnSnapshotPainted(paintedModel);
        }
        else if (request.OnSnapshotMissing is not null)
        {
            await request.OnSnapshotMissing();
        }

        TModel? freshModel;
        try
        {
            freshModel = await request.FetchAsync();
        }
        catch (Exception ex)
        {
            // A painted snapshot stays on screen; only a surface with nothing to show reports a blocking failure.
            logger.LogError(ex, "Fresh load for snapshot {SnapshotKey} failed.", request.Key);
            return new SnapshotRefreshResult<TModel>(SnapshotRefreshOutcome.Failed, paintedModel, paintedModel is not null, ex);
        }

        if (!IsCurrent())
            return new SnapshotRefreshResult<TModel>(SnapshotRefreshOutcome.Superseded, paintedModel, paintedModel is not null);

        if (freshModel is null)
        {
            return paintedModel is null
                ? new SnapshotRefreshResult<TModel>(SnapshotRefreshOutcome.Empty, null, false)
                : new SnapshotRefreshResult<TModel>(SnapshotRefreshOutcome.Unchanged, paintedModel, true);
        }

        // Content equality runs over the rendered models, so snapshot metadata never counts as a change.
        if (paintedModel is not null && request.ContentComparer.Equals(freshModel, paintedModel))
            return new SnapshotRefreshResult<TModel>(SnapshotRefreshOutcome.Unchanged, paintedModel, true);

        if (request.OnRefreshed is not null)
            await request.OnRefreshed(freshModel);

        // Skip the write once a newer run has claimed the gate — that run owns the stored snapshot now.
        if (IsCurrent())
            await WriteSnapshotAsync(request.Key, () => snapshots.SetAsync(request.Key, request.ToSnapshot(freshModel)));

        return new SnapshotRefreshResult<TModel>(SnapshotRefreshOutcome.Refreshed, freshModel, paintedModel is not null);
    }

    private async Task<TSnapshot?> ReadSnapshotAsync<TSnapshot>(string key, string snapshotTypeName, Func<Task<TSnapshot?>> read)
        where TSnapshot : SnapshotBase
    {
        try
        {
            return await read();
        }
        catch (Exception ex)
        {
            // An unreadable snapshot must never abort the load; drop it and fall through to a plain fetch.
            logger.LogWarning(ex, "Failed to read {SnapshotType} snapshot {SnapshotKey}; continuing with fresh fetch.", snapshotTypeName, key);
            try
            {
                await snapshots.RemoveAsync(key);
            }
            catch (Exception removeEx)
            {
                logger.LogWarning(removeEx, "Failed to evict unusable snapshot {SnapshotKey}.", key);
            }
            return null;
        }
    }

    private async Task WriteSnapshotAsync(string key, Func<Task> write)
    {
        try
        {
            await write();
        }
        catch (Exception ex)
        {
            // Persistence is best-effort: the fresh data is already rendered either way.
            logger.LogWarning(ex, "Fresh data rendered but saving snapshot {SnapshotKey} failed.", key);
        }
    }
}