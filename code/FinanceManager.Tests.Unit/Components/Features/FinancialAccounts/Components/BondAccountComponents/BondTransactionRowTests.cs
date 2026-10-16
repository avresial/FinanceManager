using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.BondAccountComponents;

[Trait("Category", "Unit")]
public class BondTransactionRowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClickingRow_ExpandsDetailsAndClickingAgainCollapsesThem(bool isMobile)
    {
        await using var context = CreateContext();
        var cut = RenderRow(context, isMobile);
        var row = cut.Find("[role='button']");

        Assert.Equal("false", row.GetAttribute("aria-expanded"));
        Assert.StartsWith("Poland Bond,", row.GetAttribute("aria-label"));
        Assert.Contains("change +2 units, position 12 units", row.GetAttribute("aria-label"));

        row.Click();

        Assert.Equal("true", cut.Find("[role='button']").GetAttribute("aria-expanded"));
        Assert.Contains("Issuer name", cut.Markup);
        Assert.Contains("Unit value", cut.Markup);
        Assert.Contains("12 units", cut.Markup);
        Assert.Contains("Edit", cut.Markup);
        Assert.Contains("Remove", cut.Markup);

        cut.Find("[role='button']").Click();
        Assert.Equal("false", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }

    [Theory]
    [InlineData("Enter", false)]
    [InlineData(" ", true)]
    public async Task KeyboardActivation_ExpandsBondDetails(string key, bool isMobile)
    {
        await using var context = CreateContext();
        var cut = RenderRow(context, isMobile);

        cut.Find("[role='button']").KeyDown(key);

        Assert.Equal("true", cut.Find("[role='button']").GetAttribute("aria-expanded"));
        Assert.Contains("Issuer name", cut.Markup);
        Assert.Contains("Units", cut.Markup);
        Assert.Contains("12 units", cut.Markup);
    }

    [Fact]
    public async Task SameBondEntries_IncludeTheirPostingDateAndUnitsInAccessibleNames()
    {
        await using var context = CreateContext();
        var first = RenderRow(context, false);
        var later = RenderRow(context, false, new DateTime(2026, 2, 2, 12, 0, 0, DateTimeKind.Utc));
        var firstName = first.Find("[role='button']").GetAttribute("aria-label");
        var laterName = later.Find("[role='button']").GetAttribute("aria-label");

        Assert.NotEqual(firstName, laterName);
        Assert.Contains("change +2 units, position 12 units", firstName);
        Assert.Contains("change +2 units, position 12 units", laterName);
    }

    private static IRenderedComponent<BondTransactionRow> RenderRow(BunitContext context, bool isMobile, DateTime? postingDate = null)
    {
        var account = new BondAccount(1, 1, "Bonds");
        var entry = new BondAccountEntry(1, 7, postingDate ?? new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            12m, 2m, 1);
        var details = new BondDetails
        {
            Id = 1,
            Name = "Poland Bond",
            Issuer = "Issuer name",
            Type = default,
            Currency = DefaultCurrency.PLN,
            UnitValue = 100m,
        };

        return context.Render<BondTransactionRow>(p => p
            .Add(x => x.Account, account)
            .Add(x => x.Entry, entry)
            .Add(x => x.BondDetails, details)
            .Add(x => x.IsMobile, isMobile));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(Mock.Of<IFinancialAccountService>());
        context.Services.AddSingleton(new AccountDataSynchronizationService());
        context.Services.AddSingleton(NullLogger<BondTransactionRow>.Instance);
        return context;
    }
}