using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Domain.MoneyFlow.Entities;
using System.Runtime.CompilerServices;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class InvestmentPaycheckEstimatorCardTests
{
    // GetUninitializedObject skips the constructor and all field initializers, so the
    // injected cache service stays null. Any call that tried to fetch fresh data would
    // therefore throw a NullReferenceException — a passing test proves the rate change
    // recomputed the paycheck purely from data already held by the card.
    private static InvestmentPaycheckEstimatorCard CreateCard(InvestmentPaycheckEstimate estimate, decimal rate)
    {
        var card = (InvestmentPaycheckEstimatorCard)RuntimeHelpers.GetUninitializedObject(typeof(InvestmentPaycheckEstimatorCard));
        card._estimate = estimate;
        card._annualWithdrawalRate = rate;
        return card;
    }

    [Fact]
    public void MonthlyPaycheck_ComputesLocallyFromInvestableValueAndRate()
    {
        var card = CreateCard(new InvestmentPaycheckEstimate { InvestableAssetsValue = 120_000m }, 0.04m);

        // 120000 * 0.04 / 12 = 400
        Assert.Equal(400m, card.MonthlyPaycheck);
    }

    [Fact]
    public void OnPresetSelected_RecalculatesPaycheckLocally_WithoutFetching()
    {
        var card = CreateCard(new InvestmentPaycheckEstimate { InvestableAssetsValue = 120_000m }, 0.04m);

        card.OnPresetSelected(0.05m);

        Assert.Equal(0.05m, card._annualWithdrawalRate);
        // 120000 * 0.05 / 12 = 500
        Assert.Equal(500m, card.MonthlyPaycheck);
    }

    [Fact]
    public void OnRateChanged_RecalculatesPaycheckLocally_WithoutFetching()
    {
        var card = CreateCard(new InvestmentPaycheckEstimate { InvestableAssetsValue = 240_000m }, 0.04m);

        card.OnRateChanged(0.03m);

        Assert.Equal(0.03m, card._annualWithdrawalRate);
        // 240000 * 0.03 / 12 = 600
        Assert.Equal(600m, card.MonthlyPaycheck);
    }

    [Fact]
    public void ReplacementRatio_DerivesFromLocalPaycheckAndAverageSalary()
    {
        var card = CreateCard(new InvestmentPaycheckEstimate
        {
            InvestableAssetsValue = 120_000m,
            SalaryMonthsUsed = 3,
            AverageMonthlySalary = 800m,
        }, 0.04m);

        // paycheck 400 / salary 800 = 0.5
        Assert.Equal(0.5m, card.ReplacementRatio);
    }

    [Fact]
    public void ReplacementRatio_TracksRateChangesLocally()
    {
        var card = CreateCard(new InvestmentPaycheckEstimate
        {
            InvestableAssetsValue = 120_000m,
            SalaryMonthsUsed = 3,
            AverageMonthlySalary = 800m,
        }, 0.04m);

        card.OnPresetSelected(0.08m);

        // paycheck 800 / salary 800 = 1.0
        Assert.Equal(1.0m, card.ReplacementRatio);
    }

    [Fact]
    public void MonthlyPaycheck_And_ReplacementRatio_RoundToServerPrecision()
    {
        // Matches InvestmentPaycheckEstimatorService: paycheck rounded to 2 decimals,
        // ratio rounded to 4 decimals from that rounded paycheck.
        var card = CreateCard(new InvestmentPaycheckEstimate
        {
            InvestableAssetsValue = 100_000m,
            SalaryMonthsUsed = 3,
            AverageMonthlySalary = 3_000m,
        }, 0.05m);

        // 100000 * 0.05 / 12 = 416.66666... -> 416.67
        Assert.Equal(416.67m, card.MonthlyPaycheck);
        // 416.67 / 3000 = 0.138890 -> 0.1389
        Assert.Equal(0.1389m, card.ReplacementRatio);
    }

    [Fact]
    public void ReplacementRatio_IsNull_WhenNoSalaryData()
    {
        var card = CreateCard(new InvestmentPaycheckEstimate { InvestableAssetsValue = 120_000m }, 0.04m);

        Assert.Null(card.ReplacementRatio);
    }
}