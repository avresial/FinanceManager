using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

public sealed class AccountChartSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
    public Task<SnapshotRefreshResult<AccountChartModel>> RefreshCurrencyAsync(
        int userId,
        int accountId,
        int currencyId,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<AccountChartModel?>> fetchAsync,
        Func<AccountChartModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<AccountChartModel, Task>? onRefreshed = null) =>
        RefreshStandardAsync("currency", userId, accountId, currencyId, gate, claimedVersion,
            fetchAsync, onSnapshotPainted, onSnapshotMissing, onRefreshed);

    public Task<SnapshotRefreshResult<AccountChartModel>> RefreshBondAsync(
        int userId,
        int accountId,
        int currencyId,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<AccountChartModel?>> fetchAsync,
        Func<AccountChartModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<AccountChartModel, Task>? onRefreshed = null) =>
        RefreshStandardAsync("bond", userId, accountId, currencyId, gate, claimedVersion,
            fetchAsync, onSnapshotPainted, onSnapshotMissing, onRefreshed);

    public Task<SnapshotRefreshResult<InvestmentAccountChartModel>> RefreshInvestmentAsync(
        int userId,
        int accountId,
        int currencyId,
        long? benchmarkListingId,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<InvestmentAccountChartModel?>> fetchAsync,
        Func<InvestmentAccountChartModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<InvestmentAccountChartModel, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<InvestmentAccountChartSnapshot, InvestmentAccountChartModel>
        {
            Key = $"account-chart:investment:{userId}:{accountId}:{currencyId}:{benchmarkListingId?.ToString() ?? "inflation"}",
            Gate = gate,
            ClaimedVersion = claimedVersion,
            ToModel = snapshot => snapshot.UserId == userId
                && snapshot.AccountId == accountId
                && snapshot.CurrencyId == currencyId
                && snapshot.BenchmarkListingId == benchmarkListingId
                ? snapshot.Model
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new InvestmentAccountChartSnapshot
            {
                UserId = userId,
                AccountId = accountId,
                CurrencyId = currencyId,
                BenchmarkListingId = benchmarkListingId,
                Model = model
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });

    private Task<SnapshotRefreshResult<AccountChartModel>> RefreshStandardAsync(
        string variant,
        int userId,
        int accountId,
        int currencyId,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<AccountChartModel?>> fetchAsync,
        Func<AccountChartModel, Task>? onSnapshotPainted,
        Func<Task>? onSnapshotMissing,
        Func<AccountChartModel, Task>? onRefreshed) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<AccountChartSnapshot, AccountChartModel>
        {
            Key = $"account-chart:{variant}:{userId}:{accountId}:{currencyId}",
            Gate = gate,
            ClaimedVersion = claimedVersion,
            ToModel = snapshot => snapshot.Variant == variant
                && snapshot.UserId == userId
                && snapshot.AccountId == accountId
                && snapshot.CurrencyId == currencyId
                ? snapshot.Model
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new AccountChartSnapshot
            {
                Variant = variant,
                UserId = userId,
                AccountId = accountId,
                CurrencyId = currencyId,
                Model = model
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });
}