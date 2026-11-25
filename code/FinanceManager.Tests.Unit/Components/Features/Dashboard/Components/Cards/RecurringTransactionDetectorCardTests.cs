using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;
using System.Web;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public class RecurringTransactionDetectorCardTests
{
    [Fact]
    public async Task SelectingAPattern_LoadsItsEntriesInOneOwnerRequest_NewestFirst_ForUnlinkedEntries()
    {
        var handler = new DrillDownHandler(
            Pattern("Netflix", 1, 2, 3),
            [Entry(1, new DateTime(2026, 7, 5), -49m), Entry(3, new DateTime(2026, 9, 5), -49m), Entry(2, new DateTime(2026, 8, 5), -49m)]);
        await using var context = CreateContext(handler);
        var cut = context.Render<RecurringTransactionDetectorCard>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=recurring-outgoing-group] button")));

        await cut.Find("[data-testid=recurring-outgoing-group] button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tbody tr").Count));
        var request = Assert.Single(handler.EntryRequests);
        Assert.Equal(["1", "2", "3"], HttpUtility.ParseQueryString(request.Query)["entryIds"]!.Split(',').Order());
        Assert.StartsWith("/api/transactions/cash", request.AbsolutePath, StringComparison.Ordinal);
        Assert.DoesNotContain("No entries found", cut.Markup);
        var months = cut.FindAll("tbody tr td[data-label=Date]").Select(cell => cell.TextContent.Trim()).ToList();
        DateTime[] newestFirst = [new(2026, 9, 5), new(2026, 8, 5), new(2026, 7, 5)];
        Assert.Equal(newestFirst.Select(DateFormatter.FormatDayMonth), months);
        Assert.Contains("-49.00 EUR", cut.Markup);
    }

    [Fact]
    public async Task APatternWithManyEntries_IsFetchedInChunksOfAtMost100Ids_AndMergedNewestFirst()
    {
        var ids = Enumerable.Range(1, 120).ToArray();
        var entries = ids.Select(id => Entry(id, new DateTime(2026, 1, 1).AddDays(id), -1m)).ToList();
        var handler = new DrillDownHandler(Pattern("Gym", [.. ids, .. ids.Take(5)]), entries);
        await using var context = CreateContext(handler);
        var client = context.Services.GetRequiredService<CashTransactionsHttpClient>();

        var result = await client.GetByIdsAsync([.. ids, .. ids.Take(5).Select(id => (long)id)], Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.EntryRequests.Count);
        Assert.All(handler.EntryRequests, request => Assert.True(HttpUtility.ParseQueryString(request.Query).GetValues("entryIds")!.Length <= 100));
        Assert.Equal(120, result.Count);
        Assert.Equal(Enumerable.Range(1, 120).Reverse().Select(id => (long)id), result.Select(entry => entry.EntryId));
        Assert.Empty(await client.GetByIdsAsync([], Xunit.TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.EntryRequests.Count);
    }

    [Fact]
    public async Task LeavingTheDetail_BeforeTheRequestCompletes_IgnoresTheStaleAnswer()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new DrillDownHandler(Pattern("Netflix", 1), [Entry(1, new DateTime(2026, 7, 5), -49m)]) { EntryGate = completion };
        await using var context = CreateContext(handler);
        var cut = context.Render<RecurringTransactionDetectorCard>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=recurring-outgoing-group] button")));
        // The click completes with the (held) request, so it is not awaited until the gate opens.
        var click = cut.Find("[data-testid=recurring-outgoing-group] button").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Single(handler.EntryRequests));

        await cut.Find("button[aria-label='Back to recurring transactions']").ClickAsync();
        completion.SetResult();
        await click;
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(cut.FindAll("tbody tr td[data-label=Date]"));
        Assert.Contains("Netflix", cut.Markup);
    }

    [Fact]
    public async Task AFailedEntryRequest_ShowsTheRetryableError()
    {
        var handler = new DrillDownHandler(Pattern("Netflix", 1), []) { FailEntries = true };
        await using var context = CreateContext(handler);
        var cut = context.Render<RecurringTransactionDetectorCard>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=recurring-outgoing-group] button")));

        await cut.Find("[data-testid=recurring-outgoing-group] button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("Unable to load transaction details.", cut.Markup));
        handler.FailEntries = false;
        handler.Entries.Add(Entry(1, new DateTime(2026, 7, 5), -49m));
        await cut.FindAll("button").Single(button => button.TextContent.Contains("Retry", StringComparison.Ordinal)).ClickAsync();
        cut.WaitForAssertion(() => Assert.Contains("-49.00 EUR", cut.Markup));
    }

    private static RecurringTransactionResult Pattern(string name, params int[] entryIds) => new(name, -49m)
    {
        PatternId = Guid.NewGuid(),
        MonthlyCost = 49m,
        Cadence = RecurringCadence.Monthly,
        Entries = [.. entryIds.Select(id => new RecurringTransactionEntryReference(id))]
    };

    // An owner entry that is not linked to any account.
    private static CurrencyTransactionLogItemDto Entry(long id, DateTime postedAt, decimal change) =>
        new(id, postedAt, change, "Subscription", 2, "EUR", "Merchant", []);

    private static BunitContext CreateContext(DrillDownHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(() => new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });
        context.Services.AddSingleton(login.Object);
        var snapshots = new Mock<ISnapshotService>();
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton<DashboardCardsSnapshotStore>();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(handler);
        context.Services.AddSingleton(new RecurringTransactionDetectorHttpClient(http));
        context.Services.AddSingleton(new CashTransactionsHttpClient(http));
        return context;
    }

    private sealed class DrillDownHandler(RecurringTransactionResult pattern, List<CurrencyTransactionLogItemDto> entries) : HttpMessageHandler
    {
        public List<Uri> EntryRequests { get; } = [];
        public List<CurrencyTransactionLogItemDto> Entries { get; } = entries;
        public bool FailEntries { get; set; }
        public TaskCompletionSource? EntryGate { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath.Contains("/api/transactions/cash", StringComparison.Ordinal))
            {
                EntryRequests.Add(uri);
                if (EntryGate is not null) await EntryGate.Task;
                if (FailEntries) return new HttpResponseMessage(HttpStatusCode.InternalServerError);

                var wanted = HttpUtility.ParseQueryString(uri.Query).GetValues("entryIds")!.Select(long.Parse).ToHashSet();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Entries.Where(entry => wanted.Contains(entry.EntryId)).ToList()) };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<RecurringTransactionResult> { pattern }) };
        }
    }
}