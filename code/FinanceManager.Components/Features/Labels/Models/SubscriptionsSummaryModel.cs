using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Components.Features.Labels.Models;

/// <summary>
/// Rendered content of the Subscriptions page summary tiles. Derived from the active subscriptions
/// only, so muting or cancelling one changes it while the list below still shows the item.
/// </summary>
public sealed record SubscriptionsSummaryModel(int ActiveCount, decimal MonthlyCost, int IncreaseCount)
{
    /// <summary>What the tiles render for a user with no active subscriptions.</summary>
    public static SubscriptionsSummaryModel Empty { get; } = new(0, 0m, 0);

    /// <summary>Twelve times the monthly cost, as the "Annual equivalent" tile shows it.</summary>
    public decimal AnnualCost => MonthlyCost * 12m;

    /// <summary>Aggregates the tiles from a full subscription list, ignoring muted and cancelled items.</summary>
    public static SubscriptionsSummaryModel FromSubscriptions(IEnumerable<RecurringTransactionResult> subscriptions)
    {
        var active = subscriptions.Where(x => !x.IsMuted && !x.IsCancelled).ToList();
        return new SubscriptionsSummaryModel(
            active.Count,
            active.Sum(x => x.MonthlyCost),
            active.Count(x => x.PriceDelta > 0));
    }
}