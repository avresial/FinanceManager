using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Insights.Entities;

namespace FinanceManager.Components.Features.Dashboard.Services;

/// <summary>
/// Runs the stale-while-revalidate workflow for the dashboard cards that own their own request —
/// Financial insights, Recurring transactions and Transaction log: paints the stored render model,
/// always re-fetches, and re-persists only when the rendered content changed. Owns the snapshot key
/// shapes so each card scopes them identically. Each card keeps its own existing API call.
/// </summary>
/// <remarks>
/// The workflow itself lives in <see cref="ISnapshotRefreshCoordinator"/> — see docs/codebase/UI-SNAPSHOTS.md.
/// Every card here fetches from a client that throws on a failed request, which is what lets the
/// coordinator tell "the request failed, keep showing the snapshot" apart from "the user genuinely
/// has nothing", the latter clearing the card and overwriting storage.
/// </remarks>
public sealed class DashboardCardsSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
    /// <summary>Stale-while-revalidate for the Financial insights carousel, scoped to user + account + requested count.</summary>
    /// <param name="userId">Owner of the data; scopes the snapshot key.</param>
    /// <param name="count">How many insights the carousel asked for; scopes the snapshot key.</param>
    /// <param name="accountId">Account the carousel is filtered to, or <c>null</c> for all accounts; scopes the snapshot key.</param>
    /// <param name="gate">Race protection shared with the card's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. Forwarded to the
    /// coordinator so it reuses that version instead of claiming a newer one — otherwise the caller's own
    /// post-run <c>IsCurrent</c> guard (which uses the pre-claimed version) would never match. Pass <c>null</c>
    /// only when the caller has not pre-claimed.
    /// </param>
    /// <param name="fetchAsync">Loads the insights fresh from the API. Returns an empty list when there is nothing to render.</param>
    /// <param name="onSnapshotPainted">Renders the stored insights before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh insights; only invoked when they differ from what was painted.</param>
    public Task<SnapshotRefreshResult<List<FinancialInsight>>> RefreshInsightsAsync(
        int userId,
        int count,
        int? accountId,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<List<FinancialInsight>?>> fetchAsync,
        Func<List<FinancialInsight>, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<List<FinancialInsight>, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<FinancialInsightsSnapshot, List<FinancialInsight>>
        {
            Key = BuildInsightsKey(userId, accountId, count),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            // A snapshot stored for a different user, account or count is unusable; rejecting it falls back to a plain load.
            ToModel = snapshot => snapshot.UserId == userId
                && snapshot.Count == count
                && snapshot.AccountId == accountId
                ? snapshot.Insights
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new FinancialInsightsSnapshot
            {
                UserId = userId,
                Count = count,
                AccountId = accountId,
                Insights = model
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });

    /// <summary>Stale-while-revalidate for the Recurring transactions card, scoped to user.</summary>
    /// <param name="userId">Owner of the data; scopes the snapshot key.</param>
    /// <param name="gate">Race protection shared with the card's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. See
    /// <see cref="RefreshInsightsAsync"/> for why it has to be forwarded.
    /// </param>
    /// <param name="fetchAsync">Loads the detected recurring transactions fresh from the API, together with their monthly total.</param>
    /// <param name="onSnapshotPainted">Renders the stored list before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh list; only invoked when it differs from what was painted.</param>
    public Task<SnapshotRefreshResult<RecurringTransactionsCardModel>> RefreshRecurringTransactionsAsync(
        int userId,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<RecurringTransactionsCardModel?>> fetchAsync,
        Func<RecurringTransactionsCardModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<RecurringTransactionsCardModel, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<RecurringTransactionsSnapshot, RecurringTransactionsCardModel>
        {
            Key = BuildRecurringTransactionsKey(userId),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            ToModel = snapshot => snapshot.UserId == userId
                ? new(snapshot.Data, snapshot.TotalMonthlySpend)
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new RecurringTransactionsSnapshot
            {
                UserId = userId,
                Data = model.Data,
                TotalMonthlySpend = model.TotalMonthlySpend
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });

    /// <summary>Stale-while-revalidate for the Transaction log card, scoped to user + requested row count.</summary>
    /// <param name="userId">Owner of the data; scopes the snapshot key.</param>
    /// <param name="count">How many rows the card asked for; scopes the snapshot key.</param>
    /// <param name="gate">Race protection shared with the card's other reloads.</param>
    /// <param name="claimedVersion">
    /// The version the caller already claimed from <paramref name="gate"/> before this call. See
    /// <see cref="RefreshInsightsAsync"/> for why it has to be forwarded.
    /// </param>
    /// <param name="fetchAsync">Loads the latest transactions fresh from the API. Returns an empty list when there is nothing to render.</param>
    /// <param name="onSnapshotPainted">Renders the stored rows before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh rows; only invoked when they differ from what was painted.</param>
    public Task<SnapshotRefreshResult<List<TransactionLogEntryDto>>> RefreshTransactionLogAsync(
        int userId,
        int count,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<List<TransactionLogEntryDto>?>> fetchAsync,
        Func<List<TransactionLogEntryDto>, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<List<TransactionLogEntryDto>, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<TransactionLogSnapshot, List<TransactionLogEntryDto>>
        {
            Key = BuildTransactionLogKey(userId, count),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            ToModel = snapshot => snapshot.UserId == userId && snapshot.Count == count
                ? snapshot.Data
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new TransactionLogSnapshot
            {
                UserId = userId,
                Count = count,
                Data = model
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });

    // Per-user keys with no date component, so a single snapshot per card is overwritten on each save.
    // Count and account are part of the key because they change what the card renders.
    private static string BuildInsightsKey(int userId, int? accountId, int count) =>
        $"financial-insights:{userId}:{accountId?.ToString() ?? "all"}:{count}";

    private static string BuildRecurringTransactionsKey(int userId) => $"recurring-transactions:{userId}";

    private static string BuildTransactionLogKey(int userId, int count) => $"transaction-log:{userId}:{count}";
}