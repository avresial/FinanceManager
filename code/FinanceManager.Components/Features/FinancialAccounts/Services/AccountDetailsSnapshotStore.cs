using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

/// <summary>
/// Runs the stale-while-revalidate workflow for account-details transaction lists: paints the
/// stored entries, always re-fetches, and re-persists only when the rendered entries changed.
/// Owns the snapshot key shape so every account-details page scopes it identically.
/// </summary>
/// <remarks>The workflow itself lives in <see cref="ISnapshotRefreshCoordinator"/> — see docs/codebase/UI-SNAPSHOTS.md.</remarks>
public sealed class AccountDetailsSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
    /// <param name="userId">Owner of the account; scopes the snapshot key.</param>
    /// <param name="accountId">Account being rendered; scopes the snapshot key.</param>
    /// <param name="gate">Race protection shared with the page's other reloads.</param>
    /// <param name="fetchAsync">Loads the account fresh from the API. Returns <c>null</c> when there is nothing to render.</param>
    /// <param name="onSnapshotPainted">Renders the stored entries before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh entries; only invoked when they differ from what was painted.</param>
    public Task<SnapshotRefreshResult<AccountDetailsModel<TEntry>>> RefreshAsync<TEntry>(
        int userId,
        int accountId,
        RefreshVersionGate gate,
        Func<Task<AccountDetailsModel<TEntry>?>> fetchAsync,
        Func<AccountDetailsModel<TEntry>, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<AccountDetailsModel<TEntry>, Task>? onRefreshed = null)
        => coordinator.RunAsync(new SnapshotRefreshRequest<AccountDetailsSnapshot<TEntry>, AccountDetailsModel<TEntry>>
        {
            Key = BuildKey(userId, accountId),
            Gate = gate,

            // A snapshot stored for another account is unusable; rejecting it falls back to a plain load.
            ToModel = snapshot => snapshot.AccountId == accountId
                ? new AccountDetailsModel<TEntry>(snapshot.UserId, snapshot.AccountId, snapshot.Name, snapshot.AccountType, snapshot.Entries)
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new AccountDetailsSnapshot<TEntry>
            {
                UserId = model.UserId,
                AccountId = model.AccountId,
                Name = model.Name,
                AccountType = model.AccountType,
                Entries = model.Entries,
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed,
        });

    private static string BuildKey(int userId, int accountId) => $"account-details:{userId}:{accountId}";
}