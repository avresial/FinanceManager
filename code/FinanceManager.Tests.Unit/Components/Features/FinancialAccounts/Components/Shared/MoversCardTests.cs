using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.TransactionHistory;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.Shared;

[Trait("Category", "Unit")]
public class MoversCardTests
{
    [Fact]
    public async Task AccountMoversCard_UsesInflowAndOutflowWording()
    {
        await using var context = CreateContext();
        var cut = context.Render<AccountMoversCard>(p => p
            .Add(x => x.TopEntries, [])
            .Add(x => x.BottomEntries, [])
            .Add(x => x.Currency, "PLN"));

        Assert.Contains("Top 5 inflows", cut.Markup);
        Assert.Contains("Bottom 5 outflows", cut.Markup);
        Assert.DoesNotContain("income", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expenses", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BondAccountMoversCard_UsesInflowAndOutflowWording()
    {
        await using var context = CreateContext();
        var cut = context.Render<BondAccountMoversCard>(p => p
            .Add(x => x.TopEntries, [])
            .Add(x => x.BottomEntries, []));

        Assert.Contains("Top 5 inflows", cut.Markup);
        Assert.Contains("Bottom 5 outflows", cut.Markup);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}