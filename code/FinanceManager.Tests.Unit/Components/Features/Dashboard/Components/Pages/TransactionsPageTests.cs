using Bunit;
using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Components.Pages;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Pages;

[Trait("Category", "Unit")]
public sealed class TransactionsPageTests
{
    private static readonly DateTime _oct15 = new(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime _oct14 = new(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _oct13 = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FirstPage_RendersRowsGroupedByDay_NewestFirst()
    {
        using var handler = new PageHandler((_, _) => Respond(new TransactionLogPage(
            [Entry(5, _oct15), Entry(4, _oct15), Entry(3, _oct14, AccountType.Bond)], null)));
        await using var context = CreateContext(handler);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() =>
        {
            var days = cut.FindAll("[data-testid='transactions-day']");
            Assert.Equal(2, days.Count);
            Assert.Contains("15 Oct", days[0].QuerySelector(".fm-txlog-day-header")!.TextContent);
            Assert.Equal(2, days[0].QuerySelectorAll("a.fm-txlog-link").Length);
            Assert.Contains("14 Oct", days[1].QuerySelector(".fm-txlog-day-header")!.TextContent);
            Assert.Single(days[1].QuerySelectorAll("a.fm-txlog-link"));
        });
        var request = Assert.Single(handler.Requests);
        var query = QueryHelpers.ParseQuery(request.Query);
        Assert.Equal("/api/TransactionLog/Page/7", request.AbsolutePath);
        Assert.Equal(TransactionsPage.PageSize.ToString(CultureInfo.InvariantCulture), query["pageSize"].ToString());
        Assert.False(query.ContainsKey("cursorDate"));
        Assert.Empty(cut.FindAll("[data-testid='transactions-load-more']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='transactions-end']"));
    }

    [Fact]
    public async Task Rows_LinkToOwningAccount_CurrencyRowsCarryTheEntryId()
    {
        using var handler = new PageHandler((_, _) => Respond(new TransactionLogPage(
            [Entry(5, _oct15, AccountType.Currency, accountId: 11), Entry(4, _oct15, AccountType.Stock, accountId: 12)], null)));
        await using var context = CreateContext(handler);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() =>
        {
            var hrefs = cut.FindAll("a.fm-txlog-link").Select(link => link.GetAttribute("href")).ToList();
            Assert.Equal(["/AccountDetails/11?entryId=5", "/AccountDetails/12"], hrefs);
        });
    }

    [Fact]
    public async Task LoadMore_AppendsNextPageUsingCursor_WithoutDuplicates()
    {
        var cursor = new TransactionLogCursor(_oct14, AccountType.Currency, 3);
        using var handler = new PageHandler((count, _) => Respond(count == 1
            ? new TransactionLogPage([Entry(5, _oct15), Entry(4, _oct15), Entry(3, _oct14)], cursor)
            : new TransactionLogPage([Entry(2, _oct14), Entry(1, _oct13)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("a.fm-txlog-link").Count));

        cut.Find("[data-testid='transactions-load-more']").Click();

        cut.WaitForAssertion(() =>
        {
            var hrefs = cut.FindAll("a.fm-txlog-link").Select(link => link.GetAttribute("href")).ToList();
            Assert.Equal(5, hrefs.Count);
            Assert.Equal(hrefs.Count, hrefs.Distinct().Count());
            // Oct 14 continues across the page boundary: still a single day group.
            Assert.Equal(3, cut.FindAll("[data-testid='transactions-day']").Count);
            Assert.Empty(cut.FindAll("[data-testid='transactions-load-more']"));
        });
        var query = QueryHelpers.ParseQuery(handler.Requests[1].Query);
        Assert.Equal(cursor.Date, ParseDate(query["cursorDate"]!));
        Assert.Equal("Currency", query["cursorAccountType"].ToString());
        Assert.Equal("3", query["cursorEntryId"].ToString());
        Assert.Equal(handler.Requests[0].Query.Split('&')[0], handler.Requests[1].Query.Split('&')[0]);
    }

    [Fact]
    public async Task ChangingRange_ResetsPagingAndRequestsFromTheStart()
    {
        var cursor = new TransactionLogCursor(_oct14, AccountType.Currency, 3);
        using var handler = new PageHandler((count, _) => Respond(count switch
        {
            1 => new TransactionLogPage([Entry(5, _oct15), Entry(3, _oct14)], cursor),
            2 => new TransactionLogPage([Entry(2, _oct14)], cursor),
            _ => new TransactionLogPage([Entry(9, _oct13)], null),
        }));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("a.fm-txlog-link").Count));
        cut.Find("[data-testid='transactions-load-more']").Click();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("a.fm-txlog-link").Count));

        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.SelectedRangeChanged.InvokeAsync("1M"));

        cut.WaitForAssertion(() =>
        {
            var hrefs = cut.FindAll("a.fm-txlog-link").Select(link => link.GetAttribute("href")).ToList();
            Assert.Equal(["/AccountDetails/10?entryId=9"], hrefs);
        });
        Assert.Equal(3, handler.Requests.Count);
        var first = QueryHelpers.ParseQuery(handler.Requests[0].Query);
        var reset = QueryHelpers.ParseQuery(handler.Requests[2].Query);
        Assert.False(reset.ContainsKey("cursorDate"));
        Assert.True(ParseDate(reset["start"]!) > ParseDate(first["start"]!), "1M starts later than the default 3M range");
        Assert.Equal(DateTimeKind.Utc, ParseDate(reset["start"]!).Kind);
    }

    [Fact]
    public async Task CustomRange_RequestsThePickedDaysInclusively()
    {
        using var handler = new PageHandler((_, _) => Respond(new TransactionLogPage([Entry(1, _oct13)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(handler.Requests));

        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.CustomDateRangeChanged.InvokeAsync(
            new DateRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31))));

        cut.WaitForAssertion(() => Assert.Equal(2, handler.Requests.Count));
        var query = QueryHelpers.ParseQuery(handler.Requests[1].Query);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ParseDate(query["start"]!));
        Assert.Equal(new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999), ParseDate(query["end"]!));
    }

    [Fact]
    public async Task ResponseOfASupersededRange_IsNotShown()
    {
        var firstResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new PageHandler((count, _) => count == 1
            ? firstResponse.Task
            : Respond(new TransactionLogPage([Entry(9, _oct13)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(handler.Requests));

        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.SelectedRangeChanged.InvokeAsync("1M"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("a.fm-txlog-link")));

        firstResponse.SetResult(JsonResponse(new TransactionLogPage([Entry(1, _oct15), Entry(2, _oct15)], null)));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        var hrefs = cut.FindAll("a.fm-txlog-link").Select(link => link.GetAttribute("href")).ToList();
        Assert.Equal(["/AccountDetails/10?entryId=9"], hrefs);
    }

    [Fact]
    public async Task WhileFirstPageLoads_ShowsSkeleton()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new PageHandler((_, _) => pending.Task);
        await using var context = CreateContext(handler);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='transactions-loading']"));
            Assert.NotEmpty(cut.FindAll(".mud-skeleton"));
            Assert.Empty(cut.FindAll("a.fm-txlog-link"));
        });
        pending.SetResult(JsonResponse(new TransactionLogPage([Entry(1, _oct15)], null)));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='transactions-loading']")));
    }

    [Fact]
    public async Task EmptyRange_ShowsEmptyState()
    {
        using var handler = new PageHandler((_, _) => Respond(new TransactionLogPage([], null)));
        await using var context = CreateContext(handler);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No transactions found", cut.Find("[data-testid='transactions-empty']").TextContent);
            Assert.Empty(cut.FindAll("[data-testid='transactions-day']"));
            Assert.Empty(cut.FindAll("[data-testid='transactions-load-more']"));
        });
    }

    [Fact]
    public async Task FailedFirstPage_ShowsErrorWithRetry_ThatReloads()
    {
        using var handler = new PageHandler((count, _) => count == 1
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))
            : Respond(new TransactionLogPage([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Couldn't load transactions", cut.Find("[data-testid='transactions-error']").TextContent));

        cut.Find("[data-testid='transactions-error'] button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='transactions-error']"));
            Assert.Single(cut.FindAll("a.fm-txlog-link"));
        });
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task TimedOutFirstPage_ShowsErrorInsteadOfLoadingForever()
    {
        using var handler = new PageHandler((_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transactions-error']")));
        Assert.Empty(cut.FindAll("[data-testid='transactions-loading']"));
    }

    [Fact]
    public async Task FailedLoadMore_KeepsShownRows_AndRetriesTheSamePage()
    {
        var cursor = new TransactionLogCursor(_oct14, AccountType.Currency, 3);
        using var handler = new PageHandler((count, _) => count switch
        {
            1 => Respond(new TransactionLogPage([Entry(5, _oct15), Entry(3, _oct14)], cursor)),
            2 => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)),
            _ => Respond(new TransactionLogPage([Entry(2, _oct14)], null)),
        });
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("a.fm-txlog-link").Count));

        cut.Find("[data-testid='transactions-load-more']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='transactions-load-more-error']")));
        Assert.Equal(2, cut.FindAll("a.fm-txlog-link").Count);

        cut.Find("[data-testid='transactions-load-more']").Click();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("a.fm-txlog-link").Count));
        Assert.Equal(handler.Requests[1].Query, handler.Requests[2].Query);
    }

    private static TransactionLogEntryDto Entry(long id, DateTime date, AccountType type = AccountType.Currency, int accountId = 10)
        => new(accountId, "Everyday account", type, id, date, id % 2 == 0 ? 25m : -12.5m, $"Row {id}");

    private static DateTime ParseDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static HttpResponseMessage JsonResponse(TransactionLogPage page) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(page) };

    private static Task<HttpResponseMessage> Respond(TransactionLogPage page) => Task.FromResult(JsonResponse(page));

    private static BunitContext CreateContext(HttpMessageHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 7,
            UserName = "guest",
            Password = string.Empty,
            UserRole = UserRole.User
        });
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(new TransactionLogHttpClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
        return context;
    }

    private sealed class PageHandler(Func<int, Uri, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly List<Uri> _requests = [];

        public IReadOnlyList<Uri> Requests
        {
            get
            {
                lock (_requests)
                    return [.. _requests];
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int count;
            lock (_requests)
            {
                _requests.Add(request.RequestUri!);
                count = _requests.Count;
            }
            return respond(count, request.RequestUri!);
        }
    }
}