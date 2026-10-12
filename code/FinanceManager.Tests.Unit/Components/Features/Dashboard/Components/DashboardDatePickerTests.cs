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
}