using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Insights.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// Last-rendered state of the dashboard Financial insights carousel. The scoping fields are stored
/// alongside the content so a snapshot saved for another user or count is rejected instead
/// of painted.
/// </summary>
public sealed class FinancialInsightsSnapshot : SnapshotBase
{
    public int UserId { get; set; }

    public int Count { get; set; }

    public List<FinancialInsight> Insights { get; set; } = [];
}