using FinanceManager.Components.Features.Administration.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;

namespace FinanceManager.Components.Features.Administration.Services;

/// <summary>
/// Runs the stale-while-revalidate workflow for the admin dashboard surfaces. Owns the snapshot key
/// shapes so each card scopes them identically, leaving the ordering, equality and race protection to
/// the coordinator — see docs/architecture/concepts/ui-snapshots.md.
/// </summary>
public sealed class AdminDashboardSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
    /// <summary>Stale-while-revalidate for the accounts count card, scoped to the authenticated admin user.</summary>
    /// <param name="userId">The admin who owns the view; scopes the snapshot key.</param>
    /// <param name="gate">Race protection shared with the card's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. Forwarded to the
    /// coordinator so it reuses that version instead of claiming a newer one — otherwise the caller's own
    /// post-run <c>IsCurrent</c> guard would never match. Pass <c>null</c> only when the caller has not pre-claimed.
    /// </param>
    /// <param name="fetchAsync">Loads the current account count from the API; returns null when no usable response came back.</param>
    /// <param name="onSnapshotPainted">Renders the stored count before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh count; only invoked when it differs from what was painted.</param>
    public Task<SnapshotRefreshResult<AdminAccountsCountCardModel>> RefreshAccountsCountAsync(
        int userId,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<int?>> fetchAsync,
        Func<AdminAccountsCountCardModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<AdminAccountsCountCardModel, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<AdminAccountsCountSnapshot, AdminAccountsCountCardModel>
        {
            Key = BuildAccountsCountKey(userId),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            // A snapshot stored for a different user is unusable; rejecting it falls back to a plain load.
            ToModel = snapshot => snapshot.UserId == userId
                ? new AdminAccountsCountCardModel(snapshot.Count)
                : null,
            FetchAsync = async () =>
            {
                var count = await fetchAsync();
                return count is null ? null : new AdminAccountsCountCardModel(count.Value);
            },
            ToSnapshot = model => new AdminAccountsCountSnapshot
            {
                UserId = userId,
                Count = model.Count
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });

    /// <summary>Stale-while-revalidate for the total users count card, scoped to the authenticated admin user.</summary>
    /// <param name="userId">The admin who owns the view; scopes the snapshot key.</param>
    /// <param name="gate">Race protection shared with the card's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. Forwarded to the
    /// coordinator so it reuses that version instead of claiming a newer one — otherwise the caller's own
    /// post-run <c>IsCurrent</c> guard would never match. Pass <c>null</c> only when the caller has not pre-claimed.
    /// </param>
    /// <param name="fetchAsync">Loads the current total users count from the API; returns null when no usable response came back.</param>
    /// <param name="onSnapshotPainted">Renders the stored count before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh count; only invoked when it differs from what was painted.</param>
    public Task<SnapshotRefreshResult<AdminUsersCountCardModel>> RefreshUsersCountAsync(
        int userId,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<int?>> fetchAsync,
        Func<AdminUsersCountCardModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<AdminUsersCountCardModel, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<AdminUsersCountSnapshot, AdminUsersCountCardModel>
        {
            Key = BuildUsersCountKey(userId),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            // A snapshot stored for a different user is unusable; rejecting it falls back to a plain load.
            ToModel = snapshot => snapshot.UserId == userId
                ? new AdminUsersCountCardModel(snapshot.Count)
                : null,
            FetchAsync = async () =>
            {
                var count = await fetchAsync();
                return count is null ? null : new AdminUsersCountCardModel(count.Value);
            },
            ToSnapshot = model => new AdminUsersCountSnapshot
            {
                UserId = userId,
                Count = model.Count
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });

    public Task<SnapshotRefreshResult<AdminTotalTrackedMoneyCardModel>> RefreshTotalTrackedMoneyAsync(
        int userId,
        RefreshVersionGate gate,
        Func<Task<decimal?>> fetchAsync,
        Func<AdminTotalTrackedMoneyCardModel, Task>? onSnapshotPainted = null,
        Func<AdminTotalTrackedMoneyCardModel, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<AdminTotalTrackedMoneySnapshot, AdminTotalTrackedMoneyCardModel>
        {
            Key = $"admin-total-tracked-money-pln:{userId}",
            // Compare the displayed cents; decimal scale and sub-cent changes do not repaint the card.
            ContentComparer = EqualityComparer<AdminTotalTrackedMoneyCardModel>.Default,
            Gate = gate,
            ToModel = snapshot => snapshot.UserId == userId
                ? new AdminTotalTrackedMoneyCardModel(decimal.Round(snapshot.Amount, 2))
                : null,
            FetchAsync = async () => await fetchAsync() is decimal amount
                ? new AdminTotalTrackedMoneyCardModel(decimal.Round(amount, 2))
                : null,
            ToSnapshot = model => new AdminTotalTrackedMoneySnapshot { UserId = userId, Amount = model.Amount },
            OnSnapshotPainted = onSnapshotPainted,
            OnRefreshed = onRefreshed
        });

    // Per-user key with no date component, so a single snapshot per card is overwritten on each save.
    private static string BuildAccountsCountKey(int userId) => $"admin-accounts-count:{userId}";

    private static string BuildUsersCountKey(int userId) => $"admin-users-count:{userId}";

    /// <summary>Stale-while-revalidate for the today's new visitors card, scoped to the authenticated admin user.</summary>
    /// <param name="userId">The admin who owns the view; scopes the snapshot key.</param>
    /// <param name="day">
    /// UTC day the caller captured once when the request started. The same value is fetched, stored and matched
    /// against the snapshot, so a request spanning midnight stays associated with the day it actually fetched.
    /// </param>
    /// <param name="gate">Race protection shared with the card's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. Forwarded to the
    /// coordinator so it reuses that version instead of claiming a newer one — otherwise the caller's own
    /// post-run <c>IsCurrent</c> guard would never match. Pass <c>null</c> only when the caller has not pre-claimed.
    /// </param>
    /// <param name="fetchAsync">Loads the new visitors count for the given day from the API; returns null when no usable response came back.</param>
    /// <param name="onSnapshotPainted">Renders the stored count before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh count; only invoked when it differs from what was painted.</param>
    public Task<SnapshotRefreshResult<AdminNewVisitorsTodayCardModel>> RefreshNewVisitorsTodayAsync(
        int userId,
        DateTime day,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<DateTime, Task<int?>> fetchAsync,
        Func<AdminNewVisitorsTodayCardModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<AdminNewVisitorsTodayCardModel, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<AdminNewVisitorsTodaySnapshot, AdminNewVisitorsTodayCardModel>
        {
            Key = BuildNewVisitorsTodayKey(userId),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            // A snapshot stored for a different user or day is unusable; rejecting it falls back to a plain load.
            ToModel = snapshot => snapshot.UserId == userId && snapshot.Day == day.Date
                ? new AdminNewVisitorsTodayCardModel(snapshot.Count)
                : null,
            FetchAsync = async () =>
            {
                var count = await fetchAsync(day.Date);
                return count is null ? null : new AdminNewVisitorsTodayCardModel(count.Value);
            },
            ToSnapshot = model => new AdminNewVisitorsTodaySnapshot
            {
                UserId = userId,
                Day = day.Date,
                Count = model.Count
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });

    // Per-user key with no date component, so a single snapshot is overwritten each save; the day lives in the snapshot.
    private static string BuildNewVisitorsTodayKey(int userId) => $"admin-new-visitors-today:{userId}";
}