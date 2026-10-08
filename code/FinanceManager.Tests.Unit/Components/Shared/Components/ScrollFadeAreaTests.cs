using Bunit;
using FinanceManager.Components.Shared.Components;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Tests.Unit.Components.Shared.Components;

[Trait("Category", "Unit")]
public class ScrollFadeAreaTests
{
    [Fact]
    public async Task Renders_ContentFollowedBySentinel_WithClassesAndStyle()
    {
        await using var context = new BunitContext();
        context.JSInterop.SetupVoid("financeManager.observeScrollFade", _ => true);

        var cut = context.Render<ScrollFadeArea>(parameters => parameters
            .Add(p => p.Class, "overflow-y-auto px-2")
            .Add(p => p.Style, "min-height:0;")
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Row</p>"))));

        var area = cut.Find("[data-testid=scroll-fade-area]");
        Assert.Equal("fm-scroll-fade overflow-y-auto px-2", area.GetAttribute("class"));
        Assert.Equal("min-height:0;", area.GetAttribute("style"));
        Assert.Equal("P", area.FirstElementChild!.TagName);
        Assert.Contains("fm-scroll-fade-sentinel", area.LastElementChild!.ClassList);
        Assert.Equal("true", area.LastElementChild.GetAttribute("aria-hidden"));
    }

    [Fact]
    public async Task ObservesOnFirstRender_AndReleasesOnDispose()
    {
        await using var context = new BunitContext();
        var observe = context.JSInterop.SetupVoid("financeManager.observeScrollFade", _ => true);
        observe.SetVoidResult();
        var unobserve = context.JSInterop.SetupVoid("financeManager.unobserveScrollFade", _ => true);
        unobserve.SetVoidResult();

        var cut = context.Render<ScrollFadeArea>();
        cut.Render();

        Assert.Single(observe.Invocations);
        Assert.Empty(unobserve.Invocations);

        await cut.Instance.DisposeAsync();
        Assert.Single(unobserve.Invocations);
    }
}