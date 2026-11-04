using ApexCharts;
using Bunit;
using FinanceManager.Components.Shared.Components.Charts;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

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

    private static List<TimeSeriesModel> Points(params decimal[] values) =>
        [.. values.Select((value, i) => new TimeSeriesModel(_start.AddDays(i), value))];

    private static async Task<ApexChartOptions<TimeSeriesModel>> RenderedOptionsAsync(List<TimeSeriesModel> points, string height)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var cut = context.Render<TimeSeriesValueCard>(p => p
            .Add(x => x.Title, "Net worth over time")
            .Add(x => x.Data, points)
            .Add(x => x.Height, height));

        return cut.FindComponent<ApexChart<TimeSeriesModel>>().Instance.Options;
    }
}