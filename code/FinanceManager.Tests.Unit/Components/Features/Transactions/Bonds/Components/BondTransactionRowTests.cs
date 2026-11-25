using Bunit;
using FinanceManager.Components.Features.Transactions.Bonds.Components.TransactionHistory;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Shared.Services;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Tests.Unit.Components.Features.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Bonds.Components;

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
        Assert.Contains("change +2 units", row.GetAttribute("aria-label"));

        row.Click();

        Assert.Equal("true", cut.Find("[role='button']").GetAttribute("aria-expanded"));
        Assert.Contains("Issuer name", cut.Markup);
        Assert.Contains("Unit value", cut.Markup);
        Assert.Contains("+2 units", cut.Markup);
        Assert.Contains("Edit", cut.Markup);
        Assert.Contains("Delete", cut.Markup);

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
        Assert.Contains("+2 units", cut.Markup);
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
        Assert.Contains("change +2 units", firstName);
        Assert.Contains("change +2 units", laterName);
    }

    [Fact]
    public async Task UnitsChange_UsesTheBodySize_WhenItIsTheOnlyValue()
    {
        await using var context = CreateContext();
        var cut = context.Render<BondTransactionRow>(p => p
            .Add(x => x.Entry, new BondEntry(1, 7, new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc), 12m, 2m, 1)));
        cut.Find("[role='button']").Click();

        var change = cut.FindAll(".mud-collapse-container .mud-typography.fm-tabular").Single(element => element.TextContent.Contains("+2"));

        Assert.Contains("mud-typography-body2", change.ClassName);
    }

    [Fact]
    public async Task Row_OffersEditAndRemove_AndRemovesThroughTheOwnerRoute()
    {
        var handler = new RecordingHttpHandler();
        await using var context = CreateContext(handler);
        var cut = context.Render<BondTransactionRow>(p => p
            .Add(x => x.Entry, new BondEntry(null, 7, new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc), 0m, 2m, 1))
            .Add(x => x.BondName, "Poland Bond"));
        cut.Find("[role='button']").Click();
        var published = 0;
        context.Services.GetRequiredService<TransactionDataSynchronizationService>().TransactionsChanged += () => published++;

        Assert.Contains(cut.FindAll("button"), button => button.TextContent.Trim() == "Edit");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Delete").Click();
        // The confirm button is labelled like the row action that opened it; it renders after the row.
        cut.FindAll("button").Last(button => button.TextContent.Trim() == "Delete").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, published));
        var request = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Delete, "/api/transactions/bonds/7"), (request.Method, request.PathAndQuery));
    }

    private static IRenderedComponent<BondTransactionRow> RenderRow(BunitContext context, bool isMobile, DateTime? postingDate = null)
    {
        var entry = new BondEntry(1, 7, postingDate ?? new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
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
            .Add(x => x.Entry, entry)
            .Add(x => x.BondDetails, details)
            .Add(x => x.IsMobile, isMobile));
    }

    private static BunitContext CreateContext(RecordingHttpHandler? handler = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(new TransactionDataSynchronizationService());
        context.Services.AddSingleton(NullLogger<BondTransactionRow>.Instance);
        var settings = new Mock<FinanceManager.Domain.Identity.Services.ISettingsService>();
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton(new BondTransactionsHttpClient((handler ?? new RecordingHttpHandler()).CreateClient()));
        return context;
    }
}