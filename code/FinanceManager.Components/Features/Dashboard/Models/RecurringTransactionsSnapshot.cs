using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>Last-rendered state of the dashboard Recurring transactions card.</summary>
public sealed class RecurringTransactionsSnapshot : SnapshotBase
{
    /// <summary>Owner of the data. A snapshot read for a different user is rejected rather than painted.</summary>
    public int UserId { get; set; }

    public List<RecurringTransactionResult> Data { get; set; } = [];

    public decimal TotalMonthlySpend { get; set; }
}