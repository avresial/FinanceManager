using FinanceManager.Components.Features.Labels.Models;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Tests.Unit.Components.Features.Labels.Models;

[Trait("Category", "Unit")]
public class SubscriptionsSummaryModelTests
{
    [Fact]
    public void FromSubscriptions_IgnoresMutedAndCancelled()
    {
        var model = SubscriptionsSummaryModel.FromSubscriptions(
        [
            Subscription("Netflix", 30m),
            Subscription("Spotify", 20m, isMuted: true),
            Subscription("Gym", 100m, isCancelled: true)
        ]);

        Assert.Equal(1, model.ActiveCount);
        Assert.Equal(30m, model.MonthlyCost);
        Assert.Equal(360m, model.AnnualCost);
    }

    [Fact]
    public void FromSubscriptions_CountsOnlyActivePriceIncreases()
    {
        var model = SubscriptionsSummaryModel.FromSubscriptions(
        [
            Subscription("Netflix", 30m, lastAmount: 35m, previousAmount: 30m),
            Subscription("Spotify", 20m, lastAmount: 20m, previousAmount: 25m),
            Subscription("Gym", 100m, lastAmount: 120m, previousAmount: 100m, isCancelled: true)
        ]);

        Assert.Equal(1, model.IncreaseCount);
    }

    [Fact]
    public void FromSubscriptions_NoActiveSubscriptions_MatchesEmpty()
    {
        var model = SubscriptionsSummaryModel.FromSubscriptions([Subscription("Netflix", 30m, isMuted: true)]);

        Assert.Equal(SubscriptionsSummaryModel.Empty, model);
    }

    private static RecurringTransactionResult Subscription(
        string name,
        decimal monthlyCost,
        decimal lastAmount = 0m,
        decimal previousAmount = 0m,
        bool isMuted = false,
        bool isCancelled = false) =>
        new(name, monthlyCost)
        {
            MonthlyCost = monthlyCost,
            LastAmount = lastAmount,
            PreviousAmount = previousAmount,
            IsMuted = isMuted,
            IsCancelled = isCancelled
        };
}