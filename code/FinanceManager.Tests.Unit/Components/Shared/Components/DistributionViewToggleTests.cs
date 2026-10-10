using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Shared.Components;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Components;

[Trait("Category", "Unit")]
public class DistributionViewToggleTests
{
    private static readonly List<NameValueResult> _two = [new("Mortgage", 75m), new("Loan", 25m)];

    [Fact]
    public async Task Render_ShowsTypeAndAccountLabels()
    {
        await using var context = CreateContext();
        var cut = context.Render<DistributionViewToggle>();

        var items = cut.FindAll(".mud-toggle-item");
        Assert.Equal(2, items.Count);
        Assert.Contains("Type", items[0].TextContent);
        Assert.Contains("Account", items[1].TextContent);
        Assert.DoesNotContain("Wallet", cut.Markup);
    }

    [Fact]
    public async Task Render_MarksOnlyCurrentValueAsSelected()
    {
        await using var context = CreateContext();
        var cut = context.Render<DistributionViewToggle>(p => p.Add(x => x.Value, DistributionViewToggle.AccountView));

        var selected = cut.FindAll(".mud-toggle-item-selected");
        Assert.Single(selected);
        Assert.Contains("Account", selected[0].TextContent);
    }

    [Fact]
    public async Task ClickingOtherItem_RaisesValueChangedWithThatView()
    {
        await using var context = CreateContext();
        string? changedTo = null;
        var cut = context.Render<DistributionViewToggle>(p => p
            .Add(x => x.Value, DistributionViewToggle.TypeView)
            .Add(x => x.ValueChanged, v => changedTo = v));

        cut.FindAll(".mud-toggle-item")[1].Click();

        Assert.Equal(DistributionViewToggle.AccountView, changedTo);
    }

    [Fact]
    public async Task ClickingSelectedItem_KeepsCurrentView()
    {
        await using var context = CreateContext();
        var raised = new List<string>();
        var cut = context.Render<DistributionViewToggle>(p => p
            .Add(x => x.Value, DistributionViewToggle.TypeView)
            .Add(x => x.ValueChanged, v => raised.Add(v)));

        cut.FindAll(".mud-toggle-item")[0].Click();

        Assert.DoesNotContain(string.Empty, raised);
        Assert.DoesNotContain(null!, raised);
        Assert.Single(cut.FindAll(".mud-toggle-item-selected"));
    }

    [Fact]
    public async Task AssetsCard_UsesAccountLabelAndSwitchesToAccountData()
    {
        await using var context = CreateContext();
        var cut = context.Render<AssetsDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, _two)
            .Add(x => x.WalletData, [new("Cash 1", 100m)]));

        Assert.DoesNotContain("Wallet", cut.Markup);
        Assert.Equal(2, cut.FindAll(".fm-legend-row").Count);

        cut.FindAll(".mud-toggle-item")[1].Click();

        // The toggle and the card update the view asynchronously, so wait for the re-render.
        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".fm-legend-row"));
            Assert.Contains("Cash 1", cut.Find(".fm-legend-name").TextContent);
        });
    }

    [Fact]
    public async Task LiabilitiesCard_UsesAccountLabelAndSwitchesToAccountData()
    {
        await using var context = CreateContext();
        var cut = context.Render<LiabilitiesDistributionOverviewCardView>(p => p
            .Add(x => x.TypeData, _two)
            .Add(x => x.AccountData, [new("Loan 1", 100m)]));

        Assert.Contains("Account", cut.FindAll(".mud-toggle-item")[1].TextContent);
        Assert.Equal(2, cut.FindAll(".fm-legend-row").Count);

        cut.FindAll(".mud-toggle-item")[1].Click();

        // The toggle and the card update the view asynchronously, so wait for the re-render.
        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".fm-legend-row"));
            Assert.Contains("Loan 1", cut.Find(".fm-legend-name").TextContent);
        });
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}