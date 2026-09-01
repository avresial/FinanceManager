using Bunit;
using FinanceManager.Components.Shared.Components;
using Microsoft.AspNetCore.Components;
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
    public async Task Renders_HeaderLabelAsTooltipTrigger()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What this card shows")
            .Add(p => p.AriaLabel, "About this card")
            .AddChildContent(builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddContent(1, "Net cash flow");
                builder.CloseElement();
            }));

        var trigger = cut.Find(".fm-card-info-trigger");
        Assert.Equal("0", trigger.GetAttribute("tabindex"));
        Assert.Equal("About this card", trigger.GetAttribute("aria-label"));
        Assert.Contains("Net cash flow", trigger.TextContent);
        Assert.DoesNotContain("mud-icon-button", cut.Markup);
        Assert.DoesNotContain("HelpOutline", cut.Markup);
    }

    [Fact]
    public async Task RendersTriggerInNormalFlowViaRootClass()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What this card shows"));

        // The MudTooltip root carries the global class used by the in-flow header affordance.
        var root = Assert.Single(cut.FindAll("div.mud-tooltip-root"));
        Assert.Contains("fm-card-info-root", root.ClassList);
        Assert.DoesNotContain("fm-card-info-anchor", root.ClassList);
    }

    [Fact]
    public async Task UsesSuppliedAriaLabel()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What this card shows")
            .Add(p => p.AriaLabel, "About investment rate card"));

        Assert.Equal("About investment rate card", cut.Find(".fm-card-info-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task FallsBackToGenericAriaLabelWhenNoneSupplied()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What this card shows"));

        Assert.Equal("About this card", cut.Find(".fm-card-info-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task BindsTooltipTextToUnderlyingMudTooltip()
    {
        await using var context = CreateContext();
        var cut = context.Render<CardInfoTooltip>(parameters => parameters
            .Add(p => p.Text, "What share of your salary you invest each month.")
            .Add(p => p.AriaLabel, "About investment rate card"));

        // The tooltip body is rendered through a RenderFragment so every explanation shares the
        // same polished heading and spacing.
        var tooltip = Assert.Single(cut.FindComponents<MudTooltip>());
        Assert.Equal(string.Empty, tooltip.Instance.Text);
        Assert.NotNull(tooltip.Instance.TooltipContent);
        Assert.Equal(Placement.Bottom, tooltip.Instance.Placement);
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
                builder.AddContent(1, "Investment paycheck");
                builder.CloseElement();
            })
            .Add(p => p.TooltipContent, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "fm-card-info-glossary-head");
                builder.AddContent(2, "What's in this card");
                builder.CloseElement();
            })));

        await cut.Find("div.mud-tooltip-root").TriggerEventAsync("onpointerenter", new PointerEventArgs());
        // The service notifies the provider asynchronously; let the update settle.
        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);
        provider.Render();

        // The supplied tooltip body is preserved with the supplied popover class, and the
        // short summary is suppressed when rich content is present.
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