using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

public sealed class AccountChartSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
    /// <summary>
    /// Builds the range component of a chart snapshot key. Chart snapshots are scoped to the range
    /// they were captured for: a "Month" series must never paint into a "1Y" view, both because the
    /// data covers the wrong window and because painting would fight the range the user just picked.
    /// A custom range additionally carries its own start/end, since the range name alone doesn't
    /// identify the window.
    /// </summary>
    public static string BuildRangeKey(string selectedRange, DateTime dateStart, DateTime dateEnd) =>
        selectedRange == DateRangeHelper.CustomRangeKey
            ? $"{selectedRange}:{dateStart:yyyyMMdd}:{dateEnd:yyyyMMdd}"
            : selectedRange;

    public Task<SnapshotRefreshResult<AccountChartModel>> RefreshCurrencyAsync(
        int userId,
        int accountId,
        int currencyId,
        string rangeKey,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<AccountChartModel?>> fetchAsync,
        Func<AccountChartModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<AccountChartModel, Task>? onRefreshed = null) =>
        RefreshStandardAsync("currency", userId, accountId, currencyId, rangeKey, gate, claimedVersion,
            fetchAsync, onSnapshotPainted, onSnapshotMissing, onRefreshed);

    public Task<SnapshotRefreshResult<AccountChartModel>> RefreshBondAsync(
        int userId,
        int accountId,
        int currencyId,
        string rangeKey,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<AccountChartModel?>> fetchAsync,
        Func<AccountChartModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<AccountChartModel, Task>? onRefreshed = null) =>
        RefreshStandardAsync("bond", userId, accountId, currencyId, rangeKey, gate, claimedVersion,
            fetchAsync, onSnapshotPainted, onSnapshotMissing, onRefreshed);

    public Task<SnapshotRefreshResult<InvestmentAccountChartModel>> RefreshInvestmentAsync(
        int userId,
        int accountId,
        int currencyId,
        long? benchmarkListingId,
        string rangeKey,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<InvestmentAccountChartModel?>> fetchAsync,
        Func<InvestmentAccountChartModel, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<InvestmentAccountChartModel, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<InvestmentAccountChartSnapshot, InvestmentAccountChartModel>
        {
            Key = $"account-chart:investment:{userId}:{accountId}:{currencyId}:{benchmarkListingId?.ToString() ?? "inflation"}:{rangeKey}",
            Gate = gate,
            ClaimedVersion = claimedVersion,
            ToModel = snapshot => snapshot.UserId == userId
                && snapshot.AccountId == accountId
                && snapshot.CurrencyId == currencyId
                && snapshot.BenchmarkListingId == benchmarkListingId
                && snapshot.RangeKey == rangeKey
                ? snapshot.Model
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new InvestmentAccountChartSnapshot
            {
                UserId = userId,
                AccountId = accountId,
                CurrencyId = currencyId,
                BenchmarkListingId = benchmarkListingId,
                RangeKey = rangeKey,
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
        string rangeKey,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<AccountChartModel?>> fetchAsync,
        Func<AccountChartModel, Task>? onSnapshotPainted,
        Func<Task>? onSnapshotMissing,
        Func<AccountChartModel, Task>? onRefreshed) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<AccountChartSnapshot, AccountChartModel>
        {
            Key = $"account-chart:{variant}:{userId}:{accountId}:{currencyId}:{rangeKey}",
            Gate = gate,
            ClaimedVersion = claimedVersion,
            ToModel = snapshot => snapshot.Variant == variant
                && snapshot.UserId == userId
                && snapshot.AccountId == accountId
                && snapshot.CurrencyId == currencyId
                && snapshot.RangeKey == rangeKey
                ? snapshot.Model
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = model => new AccountChartSnapshot
            {
                Variant = variant,
                UserId = userId,
                AccountId = accountId,
                CurrencyId = currencyId,
                RangeKey = rangeKey,
                Model = model
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });
}