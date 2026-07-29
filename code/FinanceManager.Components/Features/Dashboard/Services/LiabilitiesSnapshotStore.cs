using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;

namespace FinanceManager.Components.Features.Dashboard.Services;

/// <summary>
/// Runs the stale-while-revalidate workflow for the Liabilities-page cards: paints the stored
/// render model, always re-fetches, and re-persists only when the rendered content changed. Owns
/// the snapshot key shapes so each card scopes them identically. Each card keeps its own existing
/// API call — no aggregate liabilities request is introduced.
/// </summary>
/// <remarks>The workflow itself lives in <see cref="ISnapshotRefreshCoordinator"/> — see docs/codebase/UI-SNAPSHOTS.md.</remarks>
public sealed class LiabilitiesSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
    /// <summary>Stale-while-revalidate for the liabilities time-series chart, scoped to user + preferred currency.</summary>
    /// <param name="userId">Owner of the data; scopes the snapshot key.</param>
    /// <param name="currency">Preferred currency short name; scopes the snapshot key.</param>
    /// <param name="start">Range start the fresh model was requested for; stored for context only.</param>
    /// <param name="end">Range end the fresh model was requested for; stored for context only.</param>
    /// <param name="gate">Race protection shared with the card's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. Forwarded to the
    /// coordinator so it reuses that version instead of claiming a newer one — otherwise the caller's own
    /// post-run <c>IsCurrent</c> guard (which uses the pre-claimed version) would never match. Pass <c>null</c>
    /// only when the caller has not pre-claimed.
    /// </param>
    /// <param name="fetchAsync">Loads the chart fresh from the API. Returns an empty model when there is nothing to render.</param>
    /// <param name="onSnapshotPainted">Renders the stored series before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh series; only invoked when it differs from what was painted.</param>
    public Task<SnapshotRefreshResult<TimeSeriesCardModel>> RefreshTimeSeriesAsync(
        int userId,
        string currency,
        DateTime start,
        DateTime end,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<TimeSeriesCardModel?>> fetchAsync,
        Func<TimeSeriesCardModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<TimeSeriesCardModel, Task>? onRefreshed = null)
        => coordinator.RunAsync(new SnapshotRefreshRequest<LiabilitiesTimeSeriesSnapshot, TimeSeriesCardModel>
        {
            Key = BuildTimeSeriesKey(userId, currency),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            // A snapshot stored for a different user or currency is unusable; rejecting it falls back to a plain load.
            ToModel = snapshot => snapshot.UserId == userId && snapshot.Currency == currency
                ? new TimeSeriesCardModel(snapshot.Series)
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new LiabilitiesTimeSeriesSnapshot
            {
                UserId = userId,
                Currency = currency,
                StartDate = start,
                EndDate = end,
                Series = model.Series,
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed,
        });

    /// <summary>Stale-while-revalidate for the liabilities distribution breakdown, scoped to user + preferred currency id.</summary>
    /// <param name="userId">Owner of the data; scopes the snapshot key.</param>
    /// <param name="currencyId">Preferred currency id; scopes the snapshot key.</param>
    /// <param name="start">Range start the fresh model was requested for; stored for context only.</param>
    /// <param name="end">Range end the fresh model was requested for; stored for context only.</param>
    /// <param name="gate">Race protection shared with the card's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. Forwarded to the
    /// coordinator so it reuses that version instead of claiming a newer one — otherwise the caller's own
    /// post-run <c>IsCurrent</c> guard (which uses the pre-claimed version) would never match. Pass <c>null</c>
    /// only when the caller has not pre-claimed.
    /// </param>
    /// <param name="fetchAsync">Loads the breakdown fresh from the API. Returns an empty model when there is nothing to render.</param>
    /// <param name="onSnapshotPainted">Renders the stored breakdown before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh breakdown; only invoked when it differs from what was painted.</param>
    public Task<SnapshotRefreshResult<DistributionCardModel>> RefreshDistributionAsync(
        int userId,
        int currencyId,
        DateTime start,
        DateTime end,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<DistributionCardModel?>> fetchAsync,
        Func<DistributionCardModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<DistributionCardModel, Task>? onRefreshed = null)
        => coordinator.RunAsync(new SnapshotRefreshRequest<LiabilitiesDistributionSnapshot, DistributionCardModel>
        {
            Key = BuildDistributionKey(userId, currencyId),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            ToModel = snapshot => snapshot.UserId == userId && snapshot.CurrencyId == currencyId
                ? new DistributionCardModel(snapshot.TypeData, snapshot.AccountData)
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new LiabilitiesDistributionSnapshot
            {
                UserId = userId,
                CurrencyId = currencyId,
                StartDate = start,
                EndDate = end,
                TypeData = model.TypeData,
                AccountData = model.AccountData,
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed,
        });

    // Per-user keys with no date component, so a single snapshot per surface is overwritten on each save.
    // Currency-scoped so a snapshot saved before a preferred-currency change is never painted with new labels.
    private static string BuildTimeSeriesKey(int userId, string currency) => $"liabilities-timeseries:{userId}:{currency}";
    private static string BuildDistributionKey(int userId, int currencyId) => $"liabilities-distribution:{userId}:{currencyId}";
}