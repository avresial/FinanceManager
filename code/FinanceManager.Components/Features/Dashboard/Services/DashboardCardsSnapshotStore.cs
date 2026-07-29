using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Insights.Entities;

namespace FinanceManager.Components.Features.Dashboard.Services;

public sealed class DashboardCardsSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
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
            Key = $"financial-insights:{userId}:{accountId?.ToString() ?? "all"}:{count}",
            Gate = gate,
            ClaimedVersion = claimedVersion,
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
            Key = $"recurring-transactions:{userId}",
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
            Key = $"transaction-log:{userId}:{count}",
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
}