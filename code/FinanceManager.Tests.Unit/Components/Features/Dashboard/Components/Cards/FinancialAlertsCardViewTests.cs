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

        Assert.Contains("No alerts yet", cut.Markup);
        Assert.Equal("Alerts", cut.Find("[data-testid=alerts-create-button]").GetAttribute("href"));
        Assert.DoesNotContain("healthy", cut.Markup);
        Assert.DoesNotContain("configured", cut.Markup);
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