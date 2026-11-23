using ApexCharts;
using Bunit;
using FinanceManager.Components.Shared.Components.Charts;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Shared.Components.Charts;

[Trait("Category", "Unit")]
public class TimeSeriesValueCardTests
{
    private static readonly DateTime _start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Caption_NamesTheShownPointByDay()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        // Midday, so the local-time conversion keeps the same calendar day in any time zone.
        List<TimeSeriesModel> points = [new(new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc), 1m)];

        var cut = context.Render<TimeSeriesValueCard>(p => p
            .Add(x => x.Title, "Assets over time")
            .Add(x => x.Data, points));

        Assert.Equal("Current value \u00B7 30 Oct 2026", cut.Find(".fm-card-header-subtitle").TextContent.Trim());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, false)]
    public async Task Chart_DrawAnimationFollowsCardPreference(bool? animateChart, bool expected)
    {
        var options = await RenderedOptionsAsync(Points(1, 2), "250px", animateChart);

        Assert.Equal(expected, options.Chart.Animations.Enabled);
    }

    [Fact]
    public async Task ChangingAnimationPreference_SendsUpdatedOptionsToTheChartOnce()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = context.JSInterop.SetupModule();
        var charts = module.SetupModule("get_apexcharts", _ => true);
        var render = charts.SetupVoid("blazor_apexchart.renderChart", _ => true);
        render.SetVoidResult();
        context.Services.AddLogging();
        context.Services.AddMudServices();

        bool[] RenderedPreferences() => [.. render.Invocations.Select(call =>
        {
            using var options = JsonDocument.Parse(Assert.IsType<string>(call.Arguments[2]));
            return options.RootElement.GetProperty("chart").GetProperty("animations").GetProperty("enabled").GetBoolean();
        })];

        var points = Points(1, 2);
        var cut = context.Render<TimeSeriesValueCard>(p => p.Add(x => x.Data, points));
        cut.WaitForAssertion(() => Assert.Equal([true], RenderedPreferences()));

        cut.Render(p => p.Add(x => x.Data, points).Add(x => x.AnimateChart, false));
        cut.WaitForAssertion(() => Assert.Equal([true, false], RenderedPreferences()));

        cut.Render(p => p.Add(x => x.Data, points).Add(x => x.AnimateChart, false));
        await cut.InvokeAsync(Task.Yield);
        Assert.Equal([true, false], RenderedPreferences());
    }

    [Fact]
    public async Task YAxis_ReservesALeftGutterAndInsetsTheRightEdge()
    {
        var options = await RenderedOptionsAsync(Points(-21400, -21600, -22000), "250px");

        Assert.NotEqual(true, options.Yaxis!.Single().Floating);
        Assert.True(options.Grid!.Padding!.Right > 0);
    }

    [Fact]
    public async Task YAxis_UsesFewerTicksOnShortCardsThanTallOnes()
    {
        List<TimeSeriesModel> points = Points(10_000, 12_000, 30_000, 45_000);

        var shortTicks = (await RenderedOptionsAsync(points, "250px")).Yaxis!.Single().TickAmount;
        var tallTicks = (await RenderedOptionsAsync(points, "600px")).Yaxis!.Single().TickAmount;

        Assert.NotNull(shortTicks);
        Assert.NotNull(tallTicks);
        Assert.True(shortTicks < tallTicks, $"short card ticks {shortTicks} should be fewer than tall card ticks {tallTicks}");
    }

    [Fact]
    public async Task Tooltip_TracksTheNearestDayAnywhereOverThePlot()
    {
        var options = await RenderedOptionsAsync(Points(1, 2, 3), "250px");

        // Intersect mode would only fire on the invisible 0.1px markers.
        Assert.True(options.Tooltip!.Enabled);
        Assert.True(options.Tooltip.Shared);
        Assert.False(options.Tooltip.Intersect);
        Assert.Equal("dd MMM yyyy", options.Tooltip.X!.Format);
    }

    [Fact]
    public async Task Chart_HasNoDataPointHoverHandlersThatForceIntersectMode()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var cut = context.Render<TimeSeriesValueCard>(p => p.Add(x => x.Data, Points(1, 2, 3)));
        var chart = cut.FindComponent<ApexChart<TimeSeriesModel>>().Instance;

        Assert.False(chart.OnDataPointEnter.HasDelegate);
        Assert.False(chart.OnDataPointLeave.HasDelegate);
    }

    [Fact]
    public async Task XAxis_LabelsAreLiftedIntoTheClippedRow()
    {
        var options = await RenderedOptionsAsync(Points(1, 2, 3), "250px");

        var labels = Assert.IsType<XAxisLabels>(options.Xaxis!.Labels);
        Assert.Equal(-16, labels.OffsetY);
    }

    [Theory]
    [InlineData(80, "25.0%")]
    [InlineData(0, null)]
    public async Task PortfolioChip_LabelsUnrealizedGainAndScalesForOverlays(int baseline, string? percent)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var cut = context.Render<TimeSeriesValueCard>(p => p.Add(x => x.Data, Points(75, 100))
            .Add(x => x.SummaryValue, 100m).Add(x => x.DeltaBaseline, (decimal)baseline)
            .Add(x => x.Overlays, [new TimeSeriesOverlay("Capital", Points(115, 180), "#66bb6a")]));
        var badge = cut.Find(".fm-tsvc-delta").TextContent;
        Assert.Contains($"Unrealized gain +{100 - baseline}.00", badge);
        if (percent is null) Assert.DoesNotContain("%", badge);
        else Assert.Contains(percent, badge);
        var chart = cut.FindComponent<ApexChart<TimeSeriesModel>>();
        Assert.Equal(2, cut.FindComponents<ApexPointSeries<TimeSeriesModel>>().Count);
        Assert.True(Convert.ToDouble(chart.Instance.Options.Yaxis!.Single().Max) >= 180);
    }

    private static List<TimeSeriesModel> Points(params decimal[] values) =>
        [.. values.Select((value, i) => new TimeSeriesModel(_start.AddDays(i), value))];

    private static async Task<ApexChartOptions<TimeSeriesModel>> RenderedOptionsAsync(List<TimeSeriesModel> points, string height, bool? animateChart = null)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var cut = context.Render<TimeSeriesValueCard>(p =>
        {
            p.Add(x => x.Title, "Net worth over time")
                .Add(x => x.Data, points)
                .Add(x => x.Height, height);
            if (animateChart.HasValue)
                p.Add(x => x.AnimateChart, animateChart.Value);
        });

        return cut.FindComponent<ApexChart<TimeSeriesModel>>().Instance.Options;
    }
}