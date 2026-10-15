using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
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
}