using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Insights.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class DiversificationScoreSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public DateTime AsOfDate { get; set; }
    public required DiversificationScore Score { get; set; }
}