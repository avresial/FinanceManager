using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public sealed class FinancialAlertsCardViewTests
{
    [Fact]
    public async Task NoConfiguredAlerts_ShowsEmptyStateWithCreateLink_AndNoHealthyMessage()
    {
        await using var context = CreateContext();
        var cut = context.Render<FinancialAlertsCardView>(parameters => parameters.Add(p => p.ConfiguredCount, 0));

        AssertPhoneAutoHeight(cut);
        Assert.Contains("No alerts yet", cut.Markup);
        Assert.Equal("Alerts", cut.Find("[data-testid=alerts-create-button]").GetAttribute("href"));
        Assert.DoesNotContain("healthy", cut.Markup);
        Assert.DoesNotContain("configured", cut.Markup);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task LoadingOrErrorWithoutSummary_PreservesManageLink(bool isLoading, bool hasError)
    {
        await using var context = CreateContext();
        var cut = context.Render<FinancialAlertsCardView>(parameters => parameters
            .Add(p => p.ConfiguredCount, 0)
            .Add(p => p.IsLoading, isLoading)
            .Add(p => p.HasError, hasError));

        Assert.Equal("Alerts", cut.Find("a[aria-label='Manage alerts']").GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid=alerts-create-button]"));

        cut.Render(parameters => parameters.Add(p => p.IsLoading, false).Add(p => p.HasError, false));

        Assert.Empty(cut.FindAll("a[aria-label='Manage alerts']"));
        Assert.Equal("Alerts", cut.Find("[data-testid=alerts-create-button]").GetAttribute("href"));
    }

    private static void AssertPhoneAutoHeight(Bunit.IRenderedComponent<FinancialAlertsCardView> cut)
    {
        var card = cut.Find("[data-testid=financial-alerts-card]");
        Assert.Contains("fm-phone-auto-height", card.ClassList);
        Assert.Contains("height:300px", card.GetAttribute("style"));
    }

    [Fact]
    public async Task ConfiguredAlertsWithNoneTriggered_ShowsHealthyMessage()
    {
        await using var context = CreateContext();
        var cut = context.Render<FinancialAlertsCardView>(parameters => parameters.Add(p => p.ConfiguredCount, 2));

        Assert.Contains("All configured alerts are healthy.", cut.Markup);
        Assert.Contains("0 triggered of 2 configured", cut.Markup);
        Assert.DoesNotContain("No alerts yet", cut.Markup);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}