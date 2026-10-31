using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents;

[Trait("Category", "Unit")]
public class TransactionRowTests
{
    [Fact]
    public async Task CollapsedRow_DoesNotRenderDetailsUntilFirstExpanded()
    {
        await using var context = CreateContext();
        var cut = RenderRow(context, initiallyExpanded: false);

        Assert.DoesNotContain("Running balance", cut.Markup);
        Assert.DoesNotContain("Contractor details", cut.Markup);

        cut.Find("[role='button']").Click();

        Assert.Contains("Running balance", cut.Markup);
        Assert.Contains("Contractor details", cut.Markup);
        Assert.Contains("Edit", cut.Markup);

        // Collapsing keeps the rendered details so the collapse animation has content to shrink.
        cut.Find("[role='button']").Click();
        Assert.Contains("Running balance", cut.Markup);
    }

    [Fact]
    public async Task InitiallyExpandedRow_RendersDetailsImmediately()
    {
        await using var context = CreateContext();
        var cut = RenderRow(context, initiallyExpanded: true);

        Assert.Contains("Running balance", cut.Markup);
    }

    private static IRenderedComponent<TransactionRow> RenderRow(BunitContext context, bool initiallyExpanded)
    {
        var account = new CurrencyAccount(1, 1, "Cash");
        var entry = new CurrencyAccountEntry(1, 7, new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc), 100m, -20m)
        {
            Description = "Groceries",
            ContractorDetails = "Corner shop",
        };

        return context.Render<TransactionRow>(p => p
            .Add(x => x.Account, account)
            .Add(x => x.Entry, entry)
            .Add(x => x.Currency, "PLN")
            .Add(x => x.InitiallyExpanded, initiallyExpanded));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(Mock.Of<IFinancialAccountService>());
        context.Services.AddSingleton(new AccountDataSynchronizationService());
        context.Services.AddSingleton(NullLogger<TransactionRow>.Instance);
        return context;
    }
}