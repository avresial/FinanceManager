using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Tests.Unit.Domain.Entities;

[Trait("Category", "Unit")]
public class SavingsRateTests
{
    [Fact]
    public void GetPercentage_UsesSavedAmountAndAllowsNegativeRates()
    {
        var rate = new SavingsRate { Salary = 10_000m, Spending = 12_000m };

        Assert.Equal(-2_000m, rate.Saved);
        Assert.Equal(-0.2m, rate.GetPercentage());
    }

    [Fact]
    public void GetPercentage_WithoutSalaryIsUndefined()
    {
        var rate = new SavingsRate { Spending = 100m };

        Assert.False(rate.HasRate);
        Assert.Null(rate.GetPercentage());
    }
}