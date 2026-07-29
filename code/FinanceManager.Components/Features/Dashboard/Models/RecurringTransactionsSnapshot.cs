using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class RecurringTransactionsSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public List<RecurringTransactionResult> Data { get; set; } = [];
    public decimal TotalMonthlySpend { get; set; }
}

public sealed record RecurringTransactionsCardModel(
    List<RecurringTransactionResult> Data,
    decimal TotalMonthlySpend);