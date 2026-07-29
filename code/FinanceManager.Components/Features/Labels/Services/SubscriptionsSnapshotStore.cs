using FinanceManager.Components.Features.Labels.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Components.Features.Labels.Services;

/// <summary>
/// Runs the stale-while-revalidate workflow for the two surfaces of the Subscriptions page — the
/// summary tiles and the subscription list — each with its own snapshot, its own key and its own
/// content equality, so a change that only affects one of them repaints and rewrites only that one.
/// Owns the key shapes so both surfaces scope them identically.
/// </summary>
/// <remarks>
/// The workflow itself lives in <see cref="ISnapshotRefreshCoordinator"/> — see docs/codebase/UI-SNAPSHOTS.md.
/// The page feeds both surfaces from the single subscriptions request it already made; the store
/// never fetches, so no extra or aggregate request is introduced by splitting the snapshots.
/// </remarks>
public sealed class SubscriptionsSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
    /// <summary>Stale-while-revalidate for the summary tiles, scoped to user + preferred currency.</summary>
    /// <param name="userId">Owner of the data; scopes the snapshot key.</param>
    /// <param name="currency">Preferred currency short name; scopes the snapshot key.</param>
    /// <param name="gate">Race protection shared with the page's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. Forwarded to the
    /// coordinator so it reuses that version instead of claiming a newer one — otherwise the caller's own
    /// post-run <c>IsCurrent</c> guard (which uses the pre-claimed version) would never match, and the page's
    /// two surfaces would supersede each other. Pass <c>null</c> only when the caller has not pre-claimed.
    /// </param>
    /// <param name="fetchAsync">Produces the fresh totals. Returns <see cref="SubscriptionsSummaryModel.Empty"/> when there is nothing to render.</param>
    /// <param name="onSnapshotPainted">Renders the stored totals before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh totals; only invoked when they differ from what was painted.</param>
    public Task<SnapshotRefreshResult<SubscriptionsSummaryModel>> RefreshSummaryAsync(
        int userId,
        string currency,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<SubscriptionsSummaryModel?>> fetchAsync,
        Func<SubscriptionsSummaryModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<SubscriptionsSummaryModel, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<SubscriptionsSummarySnapshot, SubscriptionsSummaryModel>
        {
            Key = BuildSummaryKey(userId, currency),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            // A snapshot stored for a different user or currency is unusable; rejecting it falls back to a plain load.
            ToModel = snapshot => snapshot.UserId == userId && snapshot.Currency == currency
                ? new SubscriptionsSummaryModel(snapshot.ActiveCount, snapshot.MonthlyCost, snapshot.IncreaseCount)
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new SubscriptionsSummarySnapshot
            {
                UserId = userId,
                Currency = currency,
                ActiveCount = model.ActiveCount,
                MonthlyCost = model.MonthlyCost,
                IncreaseCount = model.IncreaseCount,
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed,
        });

    /// <summary>Stale-while-revalidate for the subscription list, scoped to user + preferred currency.</summary>
    /// <param name="userId">Owner of the data; scopes the snapshot key.</param>
    /// <param name="currency">Preferred currency short name; scopes the snapshot key.</param>
    /// <param name="gate">Race protection shared with the page's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. See
    /// <see cref="RefreshSummaryAsync"/> for why it has to be forwarded.
    /// </param>
    /// <param name="fetchAsync">Produces the fresh subscriptions. Returns an empty list when the user has none.</param>
    /// <param name="onSnapshotPainted">Renders the stored subscriptions before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh subscriptions; only invoked when they differ from what was painted.</param>
    public Task<SnapshotRefreshResult<List<RecurringTransactionResult>>> RefreshListAsync(
        int userId,
        string currency,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<List<RecurringTransactionResult>?>> fetchAsync,
        Func<List<RecurringTransactionResult>, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<List<RecurringTransactionResult>, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<SubscriptionsListSnapshot, List<RecurringTransactionResult>>
        {
            Key = BuildListKey(userId, currency),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            ToModel = snapshot => snapshot.UserId == userId && snapshot.Currency == currency
                ? snapshot.Subscriptions
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new SubscriptionsListSnapshot
            {
                UserId = userId,
                Currency = currency,
                Subscriptions = model,
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed,
        });

    // Separate per-surface keys so the tiles and the list never read or overwrite each other's entry.
    // Currency-scoped so a snapshot saved before a preferred-currency change is never painted with new labels.
    private static string BuildSummaryKey(int userId, string currency) => $"subscriptions-summary:{userId}:{currency}";

    private static string BuildListKey(int userId, string currency) => $"subscriptions-list:{userId}:{currency}";
}