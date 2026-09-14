using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Tests.Unit.Components.Shared.Helpers;

[Trait("Category", "Unit")]
public class ChartHelperTests
{
    [Fact]
    public void EnsureSeriesEndsAt_CarriesCapitalThroughTheRequestedEndAfterFinalTransaction()
    {
        List<TimeSeriesModel> series =
        [
            new(new DateTime(2026, 1, 1), 100m, "Capital"),
            new(new DateTime(2026, 2, 1), 125m, "Capital"),
        ];

        var result = ChartHelper.EnsureSeriesEndsAt(series, new DateTime(2026, 3, 31));

        Assert.Equal(3, result.Count);
        Assert.Equal(new DateTime(2026, 3, 31), result[^1].DateTime);
        Assert.Equal(125m, result[^1].Value);
        Assert.Equal("Capital", result[^1].Name);
        Assert.Equal(2, series.Count);
        Assert.Equal(new DateTime(2026, 2, 1), series[^1].DateTime);
        Assert.Equal(125m, series[^1].Value);
    }

    [Fact]
    public void EnsureSeriesEndsAt_DoesNotAddDuplicateWhenEndAlreadyExists()
    {
        List<TimeSeriesModel> series =
        [
            new(new DateTime(2026, 3, 30), 100m),
            new(new DateTime(2026, 3, 31), 125m),
        ];

        var result = ChartHelper.EnsureSeriesEndsAt(series, new DateTime(2026, 3, 31, 23, 59, 59));

        Assert.Equal(2, result.Count);
        Assert.Same(series[0], result[0]);
        Assert.Same(series[1], result[1]);
    }

    [Fact]
    public void EnsureSeriesEndsAt_LeavesEmptySeriesEmpty()
    {
        Assert.Empty(ChartHelper.EnsureSeriesEndsAt([], new DateTime(2026, 3, 31)));
    }

    /// <summary>
    /// Verifies that sparse series carry their latest values across the combined timeline.
    /// </summary>
    [Fact]
    public void AlignSeries_CarriesPreviousValuesAcrossCombinedTimeline()
    {
        List<TimeSeriesModel> balance =
        [
            new(new DateTime(2026, 1, 1), 100m, "Balance"),
            new(new DateTime(2026, 1, 3), 130m, "Balance"),
        ];
        List<TimeSeriesModel> benchmark =
        [
            new(new DateTime(2026, 1, 1), 90m, "Benchmark"),
            new(new DateTime(2026, 1, 2), 95m, "Benchmark"),
        ];

        var result = ChartHelper.AlignSeries([balance, benchmark]);

        Assert.Equal(2, result.Count);
        Assert.Equal([new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), new DateTime(2026, 1, 3)],
            result[0].Select(point => point.DateTime));
        Assert.Equal([100m, 100m, 130m], result[0].Select(point => point.Value));
        Assert.Equal([90m, 95m, 95m], result[1].Select(point => point.Value));
    }

    /// <summary>
    /// Verifies that alignment reuses source points while preserving an empty series.
    /// </summary>
    [Fact]
    public void AlignSeries_PreservesActualPointsAndLeavesEmptyInputEmpty()
    {
        var actual = new TimeSeriesModel(new DateTime(2026, 1, 2), 125m, "Balance");

        var result = ChartHelper.AlignSeries([
            new[] { actual },
            Array.Empty<TimeSeriesModel>(),
            new[] { new TimeSeriesModel(new DateTime(2026, 1, 1), 50m, "Capital") }
        ]);

        Assert.Same(actual, result[0][1]);
        Assert.Empty(result[1]);
        Assert.Equal(2, result[2].Count);
        Assert.Equal([50m, 50m], result[2].Select(point => point.Value));
        Assert.Equal(125m, result[0][0].Value);
    }

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