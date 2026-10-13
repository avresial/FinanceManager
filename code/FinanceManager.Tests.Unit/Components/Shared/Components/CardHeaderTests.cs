using Bunit;
using FinanceManager.Components.Shared.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Components;

[Trait("Category", "Unit")]
public class CardHeaderTests
{
    [Fact]
    public async Task TitleOnly_RendersOnlyTheTitle()
    {
        await using var context = CreateContext();

        var cut = context.Render<CardHeader>(parameters => parameters.Add(header => header.Title, "Transaction log"));

        var title = cut.Find("h2.fm-card-header-title");
        Assert.Equal("Transaction log", title.TextContent.Trim());
        Assert.Empty(cut.FindAll(".fm-card-header-subtitle"));
        Assert.Empty(cut.FindAll(".fm-card-header-figure"));
        Assert.Empty(cut.FindAll(".fm-card-header-badge"));
        Assert.Empty(cut.FindAll(".fm-card-header-actions"));
        Assert.Empty(cut.FindAll(".fm-card-header-leading"));
        Assert.Empty(cut.FindAll(".fm-card-info-trigger"));
    }

    [Fact]
    public async Task InfoText_WrapsTitleInTooltipTriggerWithAriaLabel()
    {
        await using var context = CreateContext();

        var cut = context.Render<CardHeader>(parameters => parameters
            .Add(header => header.Title, "Net worth")
            .Add(header => header.InfoText, "Everything you own minus everything you owe.")
            .Add(header => header.InfoAriaLabel, "About net worth card"));

        var trigger = cut.Find(".fm-card-info-trigger");
        Assert.Equal("About net worth card", trigger.GetAttribute("aria-label"));
        Assert.Contains("Net worth", trigger.QuerySelector(".fm-card-header-title")!.TextContent);
    }

    [Fact]
    public async Task Subtitle_RendersMutedContextLine()
    {
        await using var context = CreateContext();

        var cut = context.Render<CardHeader>(parameters => parameters
            .Add(header => header.Title, "Cash flow forecast")
            .Add(header => header.Subtitle, "Next 90 days"));

        Assert.Equal("Next 90 days", cut.Find(".fm-card-header-subtitle").TextContent.Trim());
    }

    [Fact]
    public async Task SubtitleContent_WinsOverSubtitle()
    {
        await using var context = CreateContext();

        var cut = context.Render<CardHeader>(parameters => parameters
            .Add(header => header.Title, "Alerts")
            .Add(header => header.Subtitle, "plain")
            .Add(header => header.SubtitleContent, Markup("<em id=\"rich\">1 triggered of 2 configured</em>")));

        var subtitle = Assert.Single(cut.FindAll(".fm-card-header-subtitle"));
        Assert.Equal("1 triggered of 2 configured", subtitle.QuerySelector("#rich")!.TextContent);
        Assert.DoesNotContain("plain", subtitle.TextContent);
    }

    [Fact]
    public async Task KeyFigureAndBadge_RenderInsideFigure()
    {
        await using var context = CreateContext();

        var cut = context.Render<CardHeader>(parameters => parameters
            .Add(header => header.Title, "Net worth")
            .Add(header => header.KeyFigure, Markup("<span id=\"value\">1 000 PLN</span>"))
            .Add(header => header.KeyFigureBadge, Markup("<span id=\"delta\">+5%</span>")));

        var figure = cut.Find(".fm-card-header-figure");
        Assert.Equal("1 000 PLN", figure.QuerySelector("#value")!.TextContent);
        Assert.Equal("+5%", figure.QuerySelector(".fm-card-header-badge #delta")!.TextContent);
    }

    [Fact]
    public async Task KeyFigureWithoutBadge_OmitsBadge()
    {
        await using var context = CreateContext();

        var cut = context.Render<CardHeader>(parameters => parameters
            .Add(header => header.Title, "Net worth")
            .Add(header => header.KeyFigure, Markup("<span>1 000 PLN</span>")));

        Assert.Single(cut.FindAll(".fm-card-header-figure"));
        Assert.Empty(cut.FindAll(".fm-card-header-badge"));
    }

    [Fact]
    public async Task Actions_RenderInActionsSlot()
    {
        await using var context = CreateContext();

        var cut = context.Render<CardHeader>(parameters => parameters
            .Add(header => header.Title, "Alerts")
            .Add(header => header.Actions, Markup("<button id=\"act\">View all</button>")));

        Assert.Equal("View all", cut.Find(".fm-card-header-actions #act").TextContent);
        Assert.Empty(cut.FindAll(".fm-card-header-figure"));
    }

    [Fact]
    public async Task Leading_RendersBeforeTitle()
    {
        await using var context = CreateContext();

        var cut = context.Render<CardHeader>(parameters => parameters
            .Add(header => header.Title, "Detail")
            .Add(header => header.Leading, Markup("<button id=\"back\">Back</button>")));

        Assert.Equal("Back", cut.Find(".fm-card-header-leading #back").TextContent);
        Assert.Equal("fm-card-header-leading", cut.Find(".fm-card-header").FirstElementChild!.ClassName);
    }

    [Fact]
    public async Task Class_IsAppendedToRoot()
    {
        await using var context = CreateContext();

        var cut = context.Render<CardHeader>(parameters => parameters
            .Add(header => header.Title, "Insights")
            .Add(header => header.Class, "pb-0"));

        var root = cut.Find("[data-testid=card-header]");
        Assert.Contains("fm-card-header", root.ClassList);
        Assert.Contains("pb-0", root.ClassList);
    }

    private static RenderFragment Markup(string html) => builder => builder.AddMarkupContent(0, html);

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}