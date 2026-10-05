using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Components.Features.Labels.Models;

/// <summary>View-only filtering and ordering of the subscriptions list. Never changes what is stored.</summary>
public static class SubscriptionListFilter
{
    public static List<RecurringTransactionResult> Apply(
        IEnumerable<RecurringTransactionResult> subscriptions,
        bool showInactive,
        RecurringCadence? cadence) =>
        subscriptions
            .Where(x => showInactive || (!x.IsMuted && !x.IsCancelled))
            .Where(x => cadence is null || x.Cadence == cadence)
            .OrderByDescending(x => x.IsFlaggedForReview)
            .ThenBy(x => x.NextExpectedChargeDate)
            .ToList();

    /// <summary>
    /// Cadences of the subscriptions that pass the inactive filter, in enum order, so a chip is never offered
    /// that would show nothing.
    /// </summary>
    public static IReadOnlyList<RecurringCadence> AvailableCadences(IEnumerable<RecurringTransactionResult> subscriptions, bool showInactive) =>
        subscriptions
            .Where(x => showInactive || (!x.IsMuted && !x.IsCancelled))
            .Select(x => x.Cadence)
            .Distinct()
            .Order()
            .ToList();
}