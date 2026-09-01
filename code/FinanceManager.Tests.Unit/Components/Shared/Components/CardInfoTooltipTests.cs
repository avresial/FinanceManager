using Bunit;
using FinanceManager.Components.Shared.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Components;

[Trait("Category", "Unit")]
public class CardInfoTooltipTests
{
    [Fact]
    public async Task Renders_OutlinedInformationIconButton()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What this card shows")
            .Add(p => p.AriaLabel, "About this card"));

        var button = cut.Find("button");
        Assert.Contains("mud-icon-button", button.ClassList);
        var svg = cut.Find("button svg");
        Assert.Contains("mud-icon-root", svg.ClassList);
        Assert.Contains("mud-svg-icon", svg.ClassList);
        // The material outlined "i" glyph is rendered verbatim from the package constant.
        Assert.Contains(Icons.Material.Outlined.Info, cut.Markup);
    }

    [Fact]
    public async Task PinsIconToCardCornerViaAnchorClass()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What this card shows"));

        // The MudTooltip root element carries the global anchor class that pins the
        // icon to the host card's top-right corner (card-info-affordance.css).
        var anchor = Assert.Single(cut.FindAll("div.mud-tooltip-root"));
        Assert.Contains("fm-card-info-anchor", anchor.ClassList);
    }

    [Fact]
    public async Task UsesSuppliedAriaLabel()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What this card shows")
            .Add(p => p.AriaLabel, "About investment rate card"));

        Assert.Equal("About investment rate card", cut.Find("button").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task FallsBackToGenericAriaLabelWhenNoneSupplied()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What this card shows"));

        Assert.Equal("About this card", cut.Find("button").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task BindsTooltipTextToUnderlyingMudTooltip()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What share of your salary you invest each month.")
            .Add(p => p.AriaLabel, "About investment rate card"));

        // MudTooltip keeps its Text parameter on the instance; the popover anchor is rendered
        // because the tooltip has text to show.
        var tooltip = Assert.Single(cut.FindComponents<MudTooltip>());
        Assert.Equal("What share of your salary you invest each month.", tooltip.Instance.Text);
        Assert.Equal(Placement.Left, tooltip.Instance.Placement);
        Assert.True(cut.FindAll("div.mud-popover-cascading-value").Count > 0);
    }

    [Fact]
    public async Task RichChildContent_RendersInTooltipBodyInsteadOfTextSummary()
    {
        await using var context = CreateContext();
        // Popover bodies render through the popover provider, so the test hosts one.
        var provider = context.Render<MudPopoverProvider>();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "Short summary")
            .Add(p => p.AriaLabel, "About investment paycheck card")
            .Add(p => p.TooltipClass, "fm-card-info-glossary")
            .AddChildContent(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "fm-card-info-glossary-head");
                builder.AddContent(2, "What's in this card");
                builder.CloseElement();
            }));

        await cut.Find("div.mud-tooltip-root").TriggerEventAsync("onpointerenter", new PointerEventArgs());
        // The service notifies the provider asynchronously; let the update settle.
        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);
        provider.Render();

        // When child content is supplied the popover body is that content (with the
        // supplied popover class), and the Text summary is suppressed.
        Assert.Contains("fm-card-info-glossary", provider.Markup);
        Assert.Contains("What's in this card", provider.Markup);
        Assert.DoesNotContain("Short summary", provider.Markup);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        // CardInfoTooltip renders a MudTooltip, which resolves MudBlazor.PopoverService — a
        // service that only implements IAsyncDisposable. Callers must dispose the context
        // with `await using`, or the synchronous container disposal throws.
        context.Services.AddMudServices();
        return context;
    }
}