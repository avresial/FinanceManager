using Bunit;
using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Shared.Helpers;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components;

[Trait("Category", "Unit")]
public class DashboardDatePickerTests
{
    [Fact]
    public async Task CustomRange_CompactLabelShowsTheDates()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        var cut = context.Render<DashboardDatePicker>(parameters => parameters
            .Add(picker => picker.StartingOption, DateRangeHelper.CustomRangeKey)
            .Add(picker => picker.InitialCustomStart, new DateTime(2026, 9, 12))
            .Add(picker => picker.InitialCustomEnd, new DateTime(2026, 10, 12))
            .Add(picker => picker.DateChanged, _ => { }));

        Assert.Equal("12 Sep – 12 Oct", cut.Find(".fm-range-label-compact").TextContent);
        Assert.Equal("12 Sep – 12 Oct 2026", cut.Find(".fm-range-label").TextContent);
    }

    [Fact]
    public async Task Trigger_RendersAsToolbarButton_WithoutFilledPillWrapper()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        var cut = context.Render<DashboardDatePicker>(parameters => parameters
            .Add(picker => picker.StartingOption, "ThisMonth")
            .Add(picker => picker.DateChanged, _ => { }));

        var trigger = cut.Find("button.fm-dashboard-datepicker-trigger");
        Assert.Equal("Date range: This month", trigger.GetAttribute("aria-label"));
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
        Assert.NotNull(trigger.QuerySelector(".mud-button-icon-start"));
        Assert.NotNull(trigger.QuerySelector(".mud-button-icon-end"));
        Assert.Empty(cut.FindAll(".mud-paper"));
    }
}