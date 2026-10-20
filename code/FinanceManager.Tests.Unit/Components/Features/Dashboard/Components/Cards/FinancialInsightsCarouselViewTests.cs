using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Domain.Insights.Entities;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public sealed class FinancialInsightsCarouselViewTests
{
    [Fact]
    public async Task Card_OptsIntoPhoneAutoHeight_AndKeepsSuppliedHeight()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        var cut = context.Render<FinancialInsightsCarouselView>(parameters => parameters.Add(p => p.Height, "411px"));
        var card = cut.Find(".mud-card");

        Assert.Contains("fm-phone-auto-height", card.ClassList);
        Assert.Contains("height:411px", card.GetAttribute("style"));
    }

    [Fact]
    public async Task SingleInsight_RendersTagsInFooter_WithoutNavigation()
    {
        await using var context = CreateContext();

        var cut = context.Render<FinancialInsightsCarouselView>(parameters => parameters
            .Add(p => p.Insights, [CreateInsight(1, "cashflow, trend")]));

        var footer = cut.Find(".ins-foot");
        Assert.Equal(["cashflow", "trend"], footer.QuerySelectorAll(".ins-tag").Select(tag => tag.TextContent));
        Assert.Empty(footer.QuerySelectorAll(".ins-arrow"));
        Assert.Empty(cut.FindAll(".ins-body .ins-tag"));
    }

    [Fact]
    public async Task MultipleInsights_RenderTagsAndNavigationInFooter()
    {
        await using var context = CreateContext();

        var cut = context.Render<FinancialInsightsCarouselView>(parameters => parameters
            .Add(p => p.Insights, [CreateInsight(1, "cashflow"), CreateInsight(2, "risk")]));

        var footer = cut.Find(".ins-foot");
        Assert.Equal(["cashflow"], footer.QuerySelectorAll(".ins-tag").Select(tag => tag.TextContent));
        Assert.Equal(2, footer.QuerySelectorAll(".ins-arrow").Length);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LoadingOrError_DoesNotRenderFooter(bool isLoading, bool hasError)
    {
        await using var context = CreateContext();

        var cut = context.Render<FinancialInsightsCarouselView>(parameters => parameters
            .Add(p => p.IsLoading, isLoading)
            .Add(p => p.HasError, hasError)
            .Add(p => p.Insights, [CreateInsight(1, "cashflow"), CreateInsight(2, "risk")]));

        Assert.Empty(cut.FindAll(".ins-foot"));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }

    private static FinancialInsight CreateInsight(int id, string tags) => new()
    {
        Id = id,
        Title = $"Insight {id}",
        Message = $"Message {id}",
        Tags = tags,
        CreatedAt = DateTime.UtcNow,
    };
}