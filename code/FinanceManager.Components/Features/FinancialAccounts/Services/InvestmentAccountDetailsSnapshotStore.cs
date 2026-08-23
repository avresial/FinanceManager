using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

/// <summary>
/// Runs the stale-while-revalidate workflow for the investment account-details trade list: paints
/// the stored trades and valuations, always re-fetches, and re-persists only when the rendered
/// content changed. Owns the snapshot key shape so every caller scopes it identically.
/// </summary>
/// <remarks>
/// The workflow itself lives in <see cref="ISnapshotRefreshCoordinator"/> — see docs/codebase/UI-SNAPSHOTS.md.
/// Investment accounts keep their own store rather than reusing <see cref="AccountDetailsSnapshotStore"/>
/// because their rows are rendered from two responses, trades and server-priced valuations, not one.
/// </remarks>
public sealed class InvestmentAccountDetailsSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
    /// <param name="userId">Owner of the account; scopes the snapshot key.</param>
    /// <param name="accountId">Account being rendered; scopes the snapshot key.</param>
    /// <param name="gate">Race protection shared with the page's other reloads.</param>
    /// <param name="fetchAsync">Loads the trades and valuations fresh from the API. Returns <c>null</c> when there is nothing to render.</param>
    /// <param name="onSnapshotPainted">Renders the stored content before the fresh request completes.</param>
    /// <param name="onSnapshotMissing">Runs instead of <paramref name="onSnapshotPainted"/> when no usable snapshot exists.</param>
    /// <param name="onRefreshed">Renders the fresh content; only invoked when it differs from what was painted.</param>
    public Task<SnapshotRefreshResult<InvestmentAccountDetailsModel>> RefreshAsync(
        int userId,
        int accountId,
        RefreshVersionGate gate,
        Func<Task<InvestmentAccountDetailsModel?>> fetchAsync,
        Func<InvestmentAccountDetailsModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<InvestmentAccountDetailsModel, Task>? onRefreshed = null,
        int? claimedVersion = null)
        => coordinator.RunAsync(new SnapshotRefreshRequest<InvestmentAccountDetailsSnapshot, InvestmentAccountDetailsModel>
        {
            Key = BuildKey(userId, accountId),
            Gate = gate,
            ClaimedVersion = claimedVersion,

            // A snapshot stored for another user or account is unusable; rejecting it falls back to a plain load.
            ToModel = snapshot => snapshot.UserId == userId && snapshot.AccountId == accountId
                ? new InvestmentAccountDetailsModel(
                    snapshot.UserId,
                    snapshot.AccountId,
                    snapshot.Name,
                    snapshot.Currency,
                    snapshot.Transactions,
                    snapshot.Valuations)
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new InvestmentAccountDetailsSnapshot
            {
                UserId = model.UserId,
                AccountId = model.AccountId,
                Name = model.Name,
                Currency = model.Currency,
                Transactions = model.Transactions,
                Valuations = model.Valuations,
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed,
        });

    private static string BuildKey(int userId, int accountId) => $"investment-account-details:{userId}:{accountId}";
}