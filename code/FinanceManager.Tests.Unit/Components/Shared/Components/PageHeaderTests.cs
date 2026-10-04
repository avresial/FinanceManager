using Bunit;
using FinanceManager.Components.Shared.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Components;

[Trait("Category", "Unit")]
public class PageHeaderTests
{
    [Fact]
    public void Renders_TitleSubtitleAndActions()
    {
        using var context = CreateContext();

        var cut = context.Render<PageHeader>(parameters => parameters
            .Add(header => header.Title, "Assets")
            .Add(header => header.Subtitle, "Everything you own.")
            .Add(header => header.Actions, (RenderFragment)(builder => builder.AddMarkupContent(0, "<button id=\"act\">Go</button>"))));

        Assert.Equal("Assets", cut.Find("h1").TextContent);
        Assert.Contains("Everything you own.", cut.Find(".fm-page-header-text").TextContent);
        Assert.Equal("Go", cut.Find(".fm-page-header-actions #act").TextContent);
    }

    [Fact]
    public void WithoutSubtitleOrActions_OmitsThem()
    {
        using var context = CreateContext();

        var cut = context.Render<PageHeader>(parameters => parameters.Add(header => header.Title, "Dashboard"));

        Assert.Equal("Dashboard", cut.Find("h1").TextContent);
        Assert.Empty(cut.FindAll(".fm-page-header-actions"));
        Assert.Single(cut.FindAll(".fm-page-header-text > *"));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}