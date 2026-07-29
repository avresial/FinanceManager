using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Insights.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class FinancialInsightsSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int Count { get; set; }
    public int? AccountId { get; set; }
    public List<FinancialInsight> Insights { get; set; } = [];
}