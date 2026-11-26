using Bunit;
using FinanceManager.Components.Features.Transactions.Cash.Components.TransactionHistory;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Shared.Services;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Tests.Unit.Components.Features.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Cash.Components;

[Trait("Category", "Unit")]
public class TransactionRowTests
{
    [Fact]
    public async Task CollapsedRow_DoesNotRenderDetailsUntilFirstExpanded()
    {
        await using var context = CreateContext();
        var cut = RenderRow(context, initiallyExpanded: false);

        Assert.DoesNotContain("Posting date", cut.Markup);
        Assert.DoesNotContain("Contractor details", cut.Markup);

        cut.Find("[role='button']").Click();

        Assert.Contains("Posting date", cut.Markup);
        Assert.Contains("Contractor details", cut.Markup);
        Assert.Contains("Edit", cut.Markup);

        // Collapsing keeps the rendered details so the collapse animation has content to shrink.
        cut.Find("[role='button']").Click();
        Assert.Contains("Posting date", cut.Markup);
    }

    [Fact]
    public async Task InitiallyExpandedRow_RendersDetailsImmediately()
    {
        await using var context = CreateContext();
        var cut = RenderRow(context, initiallyExpanded: true);

        Assert.Contains("Posting date", cut.Markup);
    }

    [Fact]
    public async Task ExpandedRow_DoesNotRepeatADescriptionTheTitleShowsInFull()
    {
        await using var context = CreateContext();
        var cut = RenderRow(context, initiallyExpanded: true);

        Assert.DoesNotContain("Description", cut.Markup);
    }

    [Fact]
    public async Task ExpandedRow_ShowsTheFullDescription_WhenTheTitleCutItOff()
    {
        await using var context = CreateContext();
        var description = new string('x', 100);
        var cut = RenderRow(context, initiallyExpanded: true, description);

        Assert.Contains("Description", cut.Markup);
        Assert.Contains(description, cut.Markup);
    }

    [Fact]
    public async Task Row_OffersEditAndDelete_AndDeletesThroughTheOwnerRoute()
    {
        var handler = new RecordingHttpHandler();
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRow>(p => p
            .Add(x => x.Entry, new CashEntry(null, 7, new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc), 0m, -20m) { Description = "Groceries" })
            .Add(x => x.Currency, "PLN")
            .Add(x => x.InitiallyExpanded, true));
        var published = 0;
        context.Services.GetRequiredService<TransactionDataSynchronizationService>().TransactionsChanged += () => published++;

        Assert.Contains(cut.FindAll("button"), button => button.TextContent.Trim() == "Edit");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Delete").Click();
        // The confirm button is labelled like the row action that opened it; it renders after the row.
        cut.FindAll("button").Last(button => button.TextContent.Trim() == "Delete").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, published));
        var request = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Delete, "/api/transactions/cash/7"), (request.Method, request.PathAndQuery));
    }

    private static IRenderedComponent<TransactionRow> RenderRow(BunitContext context, bool initiallyExpanded, string description = "Groceries")
    {
        var entry = new CashEntry(1, 7, new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc), 100m, -20m)
        {
            Description = description,
            ContractorDetails = "Corner shop",
        };

        return context.Render<TransactionRow>(p => p
            .Add(x => x.Entry, entry)
            .Add(x => x.Currency, "PLN")
            .Add(x => x.InitiallyExpanded, initiallyExpanded));
    }

    private static BunitContext CreateContext(RecordingHttpHandler? handler = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(new TransactionDataSynchronizationService());
        context.Services.AddSingleton(NullLogger<TransactionRow>.Instance);
        context.Services.AddSingleton(new CashTransactionsHttpClient((handler ?? new RecordingHttpHandler()).CreateClient()));
        return context;
    }
}