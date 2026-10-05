using FinanceManager.Components.Features.Labels.Models;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Tests.Unit.Components.Features.Labels.Models;

[Trait("Category", "Unit")]
public class SubscriptionListFilterTests
{
    private static RecurringTransactionResult Item(string name, RecurringCadence cadence, int day, bool muted = false, bool cancelled = false, bool flagged = false) =>
        new(name, 10m)
        {
            Cadence = cadence,
            NextExpectedChargeDate = new DateTime(2026, 6, day),
            IsMuted = muted,
            IsCancelled = cancelled,
            IsFlaggedForReview = flagged
        };

    [Fact]
    public void Apply_HidesInactiveUnlessRequested()
    {
        List<RecurringTransactionResult> items = [Item("a", RecurringCadence.Monthly, 1), Item("b", RecurringCadence.Monthly, 2, muted: true), Item("c", RecurringCadence.Monthly, 3, cancelled: true)];

        Assert.Equal(["a"], SubscriptionListFilter.Apply(items, false, null).Select(x => x.Name));
        Assert.Equal(3, SubscriptionListFilter.Apply(items, true, null).Count);
    }

    [Fact]
    public void Apply_FiltersByCadenceAndKeepsFlaggedFirst()
    {
        List<RecurringTransactionResult> items = [Item("a", RecurringCadence.Monthly, 1), Item("b", RecurringCadence.Annual, 2), Item("c", RecurringCadence.Monthly, 3, flagged: true)];

        Assert.Equal(["c", "a"], SubscriptionListFilter.Apply(items, false, RecurringCadence.Monthly).Select(x => x.Name));
    }

    [Fact]
    public void AvailableCadences_ListsOnlyCadencesInUse()
    {
        List<RecurringTransactionResult> items = [Item("a", RecurringCadence.Annual, 1), Item("b", RecurringCadence.Monthly, 2), Item("c", RecurringCadence.Monthly, 3)];

        Assert.Equal([RecurringCadence.Monthly, RecurringCadence.Annual], SubscriptionListFilter.AvailableCadences(items, showInactive: false));
    }

    [Fact]
    public void AvailableCadences_SkipsCadencesWithOnlyHiddenSubscriptions()
    {
        List<RecurringTransactionResult> items = [Item("a", RecurringCadence.Monthly, 1), Item("b", RecurringCadence.Annual, 2, cancelled: true)];

        Assert.Equal([RecurringCadence.Monthly], SubscriptionListFilter.AvailableCadences(items, showInactive: false));
        Assert.Equal([RecurringCadence.Monthly, RecurringCadence.Annual], SubscriptionListFilter.AvailableCadences(items, showInactive: true));
    }
}