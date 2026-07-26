using FinanceManager.Components.Shared.Components.Charts;

namespace FinanceManager.Tests.Unit.Components.Shared.Components.Charts;

[Trait("Category", "Unit")]
public class TimeSeriesValueCardTests
{
    [Fact]
    public void AddYRangePadding_AddsFivePercentBelowAndAboveDisplayedRange()
    {
        var bounds = TimeSeriesValueCard.AddYRangePadding(68_000, 72_000);

        Assert.Equal(67_800, bounds.Min);
        Assert.Equal(72_200, bounds.Max);
    }

    [Fact]
    public void AddYRangePadding_ConstantValueStillProducesVisibleRange()
    {
        var bounds = TimeSeriesValueCard.AddYRangePadding(1_000, 1_000);

        Assert.Equal(950, bounds.Min);
        Assert.Equal(1_050, bounds.Max);
    }
}