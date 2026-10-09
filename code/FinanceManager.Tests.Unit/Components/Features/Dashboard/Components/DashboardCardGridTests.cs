using Bunit;
using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components;

[Trait("Category", "Unit")]
public class DashboardCardGridTests
{
    [Fact]
    public async Task HiddenAndEmptyCards_RearrangeWithBreakpointFillers()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var visibility = new DashboardCardVisibilityService(Mock.Of<ISnapshotService>(),
            Mock.Of<ILoginService>(), NullLogger<DashboardCardVisibilityService>.Instance);
        context.Services.AddSingleton(visibility);
        await visibility.SetHiddenAsync("hidden", true, "assets");
        DashboardGridCard[] cards =
        [
            new("history", "History", new(12, 12)),
            new("distribution", "Distribution", new(4, 6)),
            new("hidden", "Hidden", new(6, 12)),
            new("empty", "Empty", new(4, 6)),
        ];
        RenderFragment<string> content = id => builder => builder.AddContent(0, id);
        var cut = context.Render<DashboardCardGrid>(parameters => parameters
            .Add(c => c.Page, "assets").Add(c => c.Cards, cards)
            .Add(c => c.CardContent, content).Add(c => c.AutoHidden, id => id == "empty"));

        var items = cut.FindComponents<MudItem>().Select(item => item.Instance).ToList();
        Assert.Equal(4, items.Count);
        Assert.Equal((12, 12, 12), (items[0].xs, items[0].md, items[0].lg));
        Assert.Equal((12, 6, 6), (items[1].xs, items[1].md, items[1].lg));
        var filler = cut.FindComponents<DashboardRowFiller>().Last().Instance;
        Assert.Equal(6, filler.MediumSpan);
        Assert.Equal(6, filler.LargeSpan);
        Assert.Equal(DashboardCardGrid.CardHeight, filler.Height);
        Assert.DoesNotContain("Hidden", cut.Markup);
        Assert.DoesNotContain("Empty", cut.Markup);
    }

    [Fact]
    public async Task AllHidden_ShowsCustomizeHintWithoutGridItems()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var visibility = new DashboardCardVisibilityService(Mock.Of<ISnapshotService>(),
            Mock.Of<ILoginService>(), NullLogger<DashboardCardVisibilityService>.Instance);
        context.Services.AddSingleton(visibility);
        await visibility.SetHiddenAsync("history", true, "liabilities");
        var cut = context.Render<DashboardCardGrid>(parameters => parameters
            .Add(c => c.Page, "liabilities")
            .Add(c => c.Cards, [new DashboardGridCard("history", "History", new(12, 12))])
            .Add(c => c.CardContent, id => builder => builder.AddContent(0, id)));
        Assert.Contains("All cards are hidden", cut.Markup);
        Assert.Empty(cut.FindComponents<MudItem>());
    }
}