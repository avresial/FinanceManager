using Bunit;
using FinanceManager.Components.Shared.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Shared.Components;

[Trait("Category", "Unit")]
public class LazyMountTests
{
    private static readonly RenderFragment _content = builder => builder.AddMarkupContent(0, "<p id=\"card\">Card</p>");

    [Fact]
    public async Task ShowsPlaceholderUntilRevealed_ThenKeepsContent()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        var observe = context.JSInterop.SetupVoid("financeManager.observeLazyMount", _ => true);
        observe.SetVoidResult();

        var cut = context.Render<LazyMount>(parameters => parameters
            .Add(p => p.Height, "390px")
            .Add(p => p.ChildContent, _content));

        Assert.Single(cut.FindAll("[data-testid=lazy-mount-placeholder]"));
        Assert.Empty(cut.FindAll("#card"));
        var invocation = Assert.Single(observe.Invocations);
        var options = JsonSerializer.SerializeToElement(invocation.Arguments[2]);
        Assert.Equal(LazyMount.PreloadMargin, options.GetProperty("margin").GetString());
        Assert.Equal(LazyMount.AutoHeightMediaQuery, options.GetProperty("autoHeightMediaQuery").GetString());
        Assert.Equal(LazyMount.AutoHeightPreloadMargin, options.GetProperty("autoHeightMargin").GetString());

        await cut.InvokeAsync(cut.Instance.Reveal);

        Assert.Single(cut.FindAll("#card"));
        Assert.Empty(cut.FindAll("[data-testid=lazy-mount-placeholder]"));
        Assert.Single(observe.Invocations);
    }

    [Fact]
    public async Task DisposeBeforeReveal_StopsObserving()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.SetupVoid("financeManager.observeLazyMount", _ => true).SetVoidResult();
        var unobserve = context.JSInterop.SetupVoid("financeManager.unobserveLazyMount", _ => true);
        unobserve.SetVoidResult();

        var cut = context.Render<LazyMount>(parameters => parameters.Add(p => p.Height, "390px"));
        await cut.Instance.DisposeAsync();

        Assert.Single(unobserve.Invocations);
    }

    [Fact]
    public async Task DisposeAfterReveal_DoesNotCallBrowser()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.SetupVoid("financeManager.observeLazyMount", _ => true).SetVoidResult();
        var unobserve = context.JSInterop.SetupVoid("financeManager.unobserveLazyMount", _ => true);

        var cut = context.Render<LazyMount>(parameters => parameters.Add(p => p.Height, "390px"));
        await cut.InvokeAsync(cut.Instance.Reveal);
        await cut.Instance.DisposeAsync();

        Assert.Empty(unobserve.Invocations);
    }
}