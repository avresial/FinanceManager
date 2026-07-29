using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// Last-rendered state of the dashboard Transaction log card. The scoping fields are stored
/// alongside the content so a snapshot saved for another user or row count is rejected instead of
/// painted.
/// </summary>
public sealed class TransactionLogSnapshot : SnapshotBase
{
    public int UserId { get; set; }

    /// <summary>Number of rows the card asked for; a snapshot for a different count is not usable.</summary>
    public int Count { get; set; }

    public List<TransactionLogEntryDto> Data { get; set; } = [];
}