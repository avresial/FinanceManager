using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Labels.Models;

/// <summary>Last-rendered state of the Subscriptions page summary tiles.</summary>
public sealed class SubscriptionsSummarySnapshot : SnapshotBase
{
    /// <summary>Owner of the data. A snapshot read for a different user is rejected rather than painted.</summary>
    public int UserId { get; set; }

    /// <summary>Currency short name the amounts were rendered with; a snapshot for another currency is rejected.</summary>
    public string Currency { get; set; } = string.Empty;

    public int ActiveCount { get; set; }

    public decimal MonthlyCost { get; set; }

    public int IncreaseCount { get; set; }
}