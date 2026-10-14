using Bunit;
using FinanceManager.Components.Shared.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Sections;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Components;

[Trait("Category", "Unit")]
public class PageHeaderTests
{
    [Fact]
    public void Renders_OneAppBarHeadingWithContentSubtitleAndActions()
    {
        using var context = CreateContext();

        var outlet = context.Render<SectionOutlet>(parameters => parameters.Add(section => section.SectionName, "page-title"));
        var cut = context.Render<PageHeader>(parameters => parameters
            .Add(header => header.Title, "Assets")
            .Add(header => header.Subtitle, "Everything you own.")
            .Add(header => header.Actions, (RenderFragment)(builder => builder.AddMarkupContent(0, "<button id=\"act\">Go</button>"))));

        Assert.Equal("Assets", Assert.Single(outlet.FindAll("h1")).TextContent);
        Assert.Equal("-1", outlet.Find("h1").GetAttribute("tabindex"));
        Assert.Empty(cut.FindAll("h1"));
        Assert.Contains("Everything you own.", cut.Find(".fm-page-header-text").TextContent);
        Assert.Equal("Go", cut.Find(".fm-page-header-actions #act").TextContent);
    }

    [Fact]
    public void WithoutSubtitleOrActions_OmitsThem()
    {
        using var context = CreateContext();

        var outlet = context.Render<SectionOutlet>(parameters => parameters.Add(section => section.SectionName, "page-title"));
        var cut = context.Render<PageHeader>(parameters => parameters.Add(header => header.Title, "Dashboard"));

        Assert.Equal("Dashboard", Assert.Single(outlet.FindAll("h1")).TextContent);
        Assert.Empty(cut.FindAll(".fm-page-header"));
        Assert.Empty(cut.FindAll(".fm-page-header-actions"));
        Assert.Empty(cut.FindAll(".fm-page-header-text"));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}