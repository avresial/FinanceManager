using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;

namespace FinanceManager.Tests.Unit.Domain.FinancialAccounts.Shared;

[Trait("Category", "Unit")]
public class GainLossTests
{
    [Theory]
    [InlineData(100, 125, 25, 25)]
    [InlineData(200, 150, -50, -25)]
    [InlineData(80, 80, 0, 0)]
    public void ComputesAmountAndPercentAgainstCostBasis(decimal costBasis, decimal currentValue, decimal expectedAmount, decimal expectedPercent)
    {
        var gainLoss = new GainLoss(costBasis, currentValue);

        Assert.Equal(expectedAmount, gainLoss.Amount);
        Assert.Equal(expectedPercent, gainLoss.Percent);
    }

    [Fact]
    public void PercentIsZeroWithoutCostBasis()
    {
        var gainLoss = new GainLoss(0m, 40m);

        Assert.Equal(40m, gainLoss.Amount);
        Assert.Equal(0m, gainLoss.Percent);
    }

    [Fact]
    public void DefaultIsZero()
    {
        GainLoss gainLoss = default;

        Assert.Equal(0m, gainLoss.Amount);
        Assert.Equal(0m, gainLoss.Percent);
    }
}