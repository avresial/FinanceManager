using FinanceManager.Application.Labels.RecurringTransactions;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Tests.Unit.Application.Labels.RecurringTransactions;

[Trait("Category", "Unit")]
public class RecurringDetectionRulesTests
{
    [Theory]
    [InlineData("Netflix.com", "NETFLIX COM", true)]
    [InlineData("Spotify", "Spotifx", true)]
    [InlineData("Spotify", "Amazon", false)]
    [InlineData("", "Amazon", false)]
    public void MerchantSimilarity_IgnoresCaseAndPunctuation(string a, string b, bool expected) =>
        Assert.Equal(expected, MerchantSimilarity.IsSameMerchant(a, b));

    [Fact]
    public void Cadence_MonthlyGaps_AreMonthly() =>
        Assert.Equal(RecurringCadence.Monthly, RecurringCadenceRules.Detect([30, 31, 28, 31]));

    [Fact]
    public void Cadence_TooFewRegularGaps_IsNull() =>
        Assert.Null(RecurringCadenceRules.Detect([30, 31, 90, 200]));

    [Theory]
    [InlineData(RecurringCadence.Weekly, 12, 52)]
    [InlineData(RecurringCadence.Quarterly, 30, 10)]
    [InlineData(RecurringCadence.Annual, 120, 10)]
    public void Cadence_ToMonthly_NormalisesPerOccurrenceAmount(RecurringCadence cadence, decimal amount, decimal expected) =>
        Assert.Equal(expected, RecurringCadenceRules.ToMonthly(amount, cadence));

    [Fact]
    public void Amounts_AreSimilarWithinFivePercent()
    {
        Assert.True(RecurringAmounts.AreSimilar(100m, 105m));
        Assert.False(RecurringAmounts.AreSimilar(100m, 105.01m));
        Assert.Equal(15m, RecurringAmounts.Median([10m, 20m, 30m, 10m]));
    }

    [Fact]
    public void Detector_FindsMonthlySubscription_AndProjectsNextCharge()
    {
        RecurringEntry[] entries =
        [
            new("Netflix", new DateTime(2026, 7, 5), 49m, 1, 1),
            new("NETFLIX", new DateTime(2026, 8, 5), 49m, 1, 2),
            new("Netflix", new DateTime(2026, 9, 5), 49m, 1, 3),
            new("Coffee", new DateTime(2026, 9, 6), 12m, 1, 4),
        ];

        var pattern = Assert.Single(RecurringPatternDetector.Detect(entries, new DateTime(2026, 9, 20), isIncome: false));

        Assert.Equal("netflix", pattern.MerchantKey);
        Assert.Equal(RecurringCadence.Monthly, pattern.Result.Cadence);
        Assert.Equal(49m, pattern.Result.MonthlyCost);
        Assert.Equal(new DateTime(2026, 10, 5), pattern.Result.NextExpectedChargeDate);
        Assert.Equal(3, pattern.Result.Entries.Count);
    }
}