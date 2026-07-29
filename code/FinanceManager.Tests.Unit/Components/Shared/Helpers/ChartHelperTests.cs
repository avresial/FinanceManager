using FinanceManager.Components.Shared.Helpers;

namespace FinanceManager.Tests.Unit.Components.Shared.Helpers;

[Trait("Category", "Unit")]
public class ChartHelperTests
{
    [Fact]
    public void AddYRangePadding_AddsFivePercentBelowAndAboveDisplayedRange()
    {
        var bounds = ChartHelper.AddYRangePadding(68_000, 72_000);

        Assert.Equal(67_800, bounds.Min);
        Assert.Equal(72_200, bounds.Max);
    }

    [Fact]
    public void AddYRangePadding_ConstantValueStillProducesVisibleRange()
    {
        var bounds = ChartHelper.AddYRangePadding(1_000, 1_000);

        Assert.Equal(950, bounds.Min);
        Assert.Equal(1_050, bounds.Max);
    }

    [Fact]
    public void GetYAxisTickFormatter_UsesCompactTicksForThousandsScaleRanges()
    {
        // A cash account swinging 28k -> 31k: ticks are ~600 apart, so "28k / 29k" reads fine.
        var formatter = ChartHelper.GetYAxisTickFormatter(28_000, 31_000, 5);

        Assert.Equal(ChartHelper.CompactCurrencyTickFormatter, formatter);
    }

    [Fact]
    public void GetYAxisTickFormatter_FallsBackToDecimalsWhenTicksAreCloserThanAHundred()
    {
        // A 300 PLN bond gaining under 1 PLN: every compact tick would collapse to "0.3k",
        // so the axis needs decimals instead.
        var formatter = ChartHelper.GetYAxisTickFormatter(299.9, 301.03, 5);

        Assert.NotEqual(ChartHelper.CompactCurrencyTickFormatter, formatter);
        Assert.Contains("maximumFractionDigits:2", formatter);
    }

    [Theory]
    [InlineData(0, 500, 0)]      // step 100 -> compact ticks, no decimals path
    [InlineData(0, 250, 0)]      // step 50
    [InlineData(0, 25, 1)]       // step 5
    [InlineData(0, 2.5, 2)]      // step 0.5
    public void GetYAxisTickFormatter_ScalesDecimalsWithTickSpacing(double min, double max, int expectedDecimals)
    {
        var formatter = ChartHelper.GetYAxisTickFormatter(min, max, 5);

        if (formatter == ChartHelper.CompactCurrencyTickFormatter) return;

        Assert.Contains($"maximumFractionDigits:{expectedDecimals}", formatter);
    }

    [Fact]
    public void GetYAxisTickFormatter_NonPositiveTickCountDoesNotDivideByZero()
    {
        var formatter = ChartHelper.GetYAxisTickFormatter(0, 1_000, 0);

        Assert.Contains("maximumFractionDigits:2", formatter);
    }

    [Fact]
    public void CompactCurrencyTickFormatter_LabelsZeroInsteadOfBlankingTheTick()
    {
        // A loan paid down to zero, or any range straddling zero, puts a real tick at 0.
        // A truthiness guard (`if(!v)`) would silently blank that gridline's label.
        Assert.DoesNotContain("if(!v)", ChartHelper.CompactCurrencyTickFormatter);
        Assert.Contains("if(v===0) return '0'", ChartHelper.CompactCurrencyTickFormatter);
    }

    [Fact]
    public void CompactCurrencyTickFormatter_StillDropsNonNumericValues()
    {
        // Guarding zero must not lose the null/undefined/NaN guard, or those render "NaNk".
        Assert.Contains("Number.isFinite(v)", ChartHelper.CompactCurrencyTickFormatter);
    }
}