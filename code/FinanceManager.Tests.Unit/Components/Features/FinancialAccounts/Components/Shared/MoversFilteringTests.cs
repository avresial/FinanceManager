using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents.TransactionHistory;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.Shared;

[Trait("Category", "Unit")]
public class MoversFilteringTests
{
    [Fact]
    public async Task AccountMoversCard_FiltersDistinctCurrencyEntriesByMode()
    {
        await using var context = CreateContext();
        var top = new CurrencyAccountEntry(1, 1, DateTime.UtcNow, 10m, 10m) { Description = "Salary deposit" };
        var bottom = new CurrencyAccountEntry(1, 2, DateTime.UtcNow, 0m, -10m) { Description = "Rent payment" };
        var cut = context.Render<AccountMoversCard>(p => p
            .Add(x => x.TopEntries, [top])
            .Add(x => x.BottomEntries, [bottom])
            .Add(x => x.Currency, "PLN"));

        Assert.Contains("All", cut.FindAll(".mud-toggle-item").Select(x => x.TextContent.Trim()));
        Assert.Contains("Salary deposit", cut.Markup);
        Assert.Contains("Rent payment", cut.Markup);

        ClickMode(cut, "Top");
        Assert.Contains("Salary deposit", cut.Markup);
        Assert.DoesNotContain("Rent payment", cut.Markup);

        ClickMode(cut, "Bottom");
        Assert.DoesNotContain("Salary deposit", cut.Markup);
        Assert.Contains("Rent payment", cut.Markup);

        ClickMode(cut, "All");
        Assert.Contains("Salary deposit", cut.Markup);
        Assert.Contains("Rent payment", cut.Markup);
    }

    [Fact]
    public async Task BondAccountMoversCard_FiltersDistinctBondEntriesByMode()
    {
        await using var context = CreateContext();
        var top = new BondAccountEntry(1, 1, DateTime.UtcNow, 10m, 10m, 1);
        var bottom = new BondAccountEntry(1, 2, DateTime.UtcNow, 0m, -10m, 2);
        var cut = context.Render<BondAccountMoversCard>(p => p
            .Add(x => x.TopEntries, [top])
            .Add(x => x.BottomEntries, [bottom])
            .Add(x => x.BondDetails, [
                new BondDetails { Id = 1, Name = "Bond purchase", Issuer = "Issuer", Type = default, Currency = DefaultCurrency.PLN },
                new BondDetails { Id = 2, Name = "Bond sale", Issuer = "Issuer", Type = default, Currency = DefaultCurrency.PLN },
            ]));

        Assert.Contains("All", cut.FindAll(".mud-toggle-item").Select(x => x.TextContent.Trim()));
        Assert.Contains("Bond purchase", cut.Markup);
        Assert.Contains("Bond sale", cut.Markup);

        ClickMode(cut, "Top");
        Assert.Contains("Bond purchase", cut.Markup);
        Assert.DoesNotContain("Bond sale", cut.Markup);

        ClickMode(cut, "Bottom");
        Assert.DoesNotContain("Bond purchase", cut.Markup);
        Assert.Contains("Bond sale", cut.Markup);

        ClickMode(cut, "All");
        Assert.Contains("Bond purchase", cut.Markup);
        Assert.Contains("Bond sale", cut.Markup);
    }

    [Fact]
    public async Task InvestmentAccountMoversCard_FiltersDistinctTradesByMode()
    {
        await using var context = CreateContext();
        var top = new InvestmentTransactionDto(1, 1, 1, 1, InvestmentTransactionType.Sell,
            2m, 50m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), null, null, "TOPTRADE", "Exchange");
        var bottom = new InvestmentTransactionDto(2, 1, 1, 2, InvestmentTransactionType.Buy,
            2m, 50m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), null, null, "BOTTOMTRADE", "Exchange");
        var cut = context.Render<InvestmentAccountMoversCard>(p => p
            .Add(x => x.TopEntries, [top])
            .Add(x => x.BottomEntries, [bottom]));

        Assert.Contains("All", cut.FindAll(".mud-toggle-item").Select(x => x.TextContent.Trim()));
        Assert.Contains("TOPTRADE", cut.Markup);
        Assert.Contains("BOTTOMTRADE", cut.Markup);

        ClickMode(cut, "Top");
        Assert.Contains("TOPTRADE", cut.Markup);
        Assert.DoesNotContain("BOTTOMTRADE", cut.Markup);

        ClickMode(cut, "Bottom");
        Assert.DoesNotContain("TOPTRADE", cut.Markup);
        Assert.Contains("BOTTOMTRADE", cut.Markup);

        ClickMode(cut, "All");
        Assert.Contains("TOPTRADE", cut.Markup);
        Assert.Contains("BOTTOMTRADE", cut.Markup);
    }

    private static void ClickMode<TComponent>(IRenderedComponent<TComponent> cut, string mode)
        where TComponent : IComponent =>
        cut.FindAll(".mud-toggle-item").Single(x => x.TextContent.Trim() == mode).Click();

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}