using Bunit;
using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Components.Pages;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Components;
using FinanceManager.Components.Features.Transactions.Investments.HttpClients;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.FinancialAccounts.Currencies;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Tests.Unit.Components.Features.Transactions;
using Microsoft.AspNetCore.Components;
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
    public async Task Rows_RenderEachEntryWithItsAccountPageRow()
    {
        using var handler = new PageHandler((_, _) => Respond(Page(
        [
            Entry(1, _oct15, TransactionKind.Cash, accountId: 10),
            Entry(2, _oct15, TransactionKind.Bond, accountId: 20),
            Entry(3, _oct15, TransactionKind.Investment, accountId: 30),
        ], null)));
        await using var context = CreateContext(handler);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Equal(["Cash", "Bond", "Investment"],
            cut.FindAll("[data-testid='transaction-history-row']").Select(row => row.GetAttribute("data-kind"))));
        Assert.Single(cut.FindComponents<TransactionRow>());
        Assert.Single(cut.FindComponents<BondTransactionRow>());
        var investment = Assert.Single(cut.FindComponents<InvestmentTransactionRow>());
        Assert.Equal(3, investment.Instance.Transaction.Id);
        Assert.Empty(cut.FindAll("a[href*=AccountDetails]"));
    }

    [Fact]
    public async Task SearchChangeResetsCursorAndKeepsAccountFilters()
    {
        using var handler = new PageHandler((count, _) => Respond(Page([Entry(count == 3 ? 4 : 5, _oct15)], new(_oct15, TransactionKind.Cash, 5))));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));
        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        Assert.Null(toolbar.Instance.SearchText);
        await cut.InvokeAsync(() => toolbar.Instance.FilterChanged.InvokeAsync(new([TransactionKind.Cash], ["PLN"])));
        cut.Find("[data-testid='transactions-load-more']").Click();
        cut.WaitForAssertion(() => Assert.True(QueryHelpers.ParseQuery(handler.Requests.Last().Query).ContainsKey("cursorDate")));
        await cut.InvokeAsync(() => toolbar.Instance.SearchTextChanged.InvokeAsync("mixed"));
        var query = QueryHelpers.ParseQuery(handler.Requests.Last().Query);
        Assert.Equal("mixed", query["search"].ToString());
        Assert.Equal("Cash", query["kinds"].ToString());
        Assert.Equal("PLN", query["currencies"].ToString());
        Assert.False(query.ContainsKey("cursorDate"));
        Assert.Single(cut.FindAll("[data-testid='transaction-history-row']"));
    }

    [Fact]
    public async Task QueryString_PrefillsSearchAndRange()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(5, _oct15)], null)));
        await using var context = CreateContext(handler);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/Transactions?search=netflix&range=1m");

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));
        var query = QueryHelpers.ParseQuery(Assert.Single(handler.Requests).Query);
        Assert.Equal("netflix", query["search"].ToString());
        var start = ParseDate(query["start"]!);
        Assert.True(start > DateTime.UtcNow.AddMonths(-2), "range=1m starts one month back, not at the default three");
        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        Assert.Equal("netflix", toolbar.Instance.SearchText);
        Assert.Equal("1M", toolbar.Instance.SelectedRange);
    }

    [Fact]
    public async Task QueryString_From_OpensACustomRangeReachingBackToThatDay()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(5, _oct15)], null)));
        await using var context = CreateContext(handler);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/Transactions?search=rent&from=2024-02-29");

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));
        var query = QueryHelpers.ParseQuery(Assert.Single(handler.Requests).Query);
        Assert.Equal(new DateTime(2024, 2, 29), ParseDate(query["start"]!).Date);
        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        Assert.Equal("Custom", toolbar.Instance.SelectedRange);
        Assert.Equal(new DateTime(2024, 2, 29), toolbar.Instance.CustomDateRange!.Start);
    }

    [Fact]
    public async Task QueryString_InvalidFrom_KeepsTheDefault()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(5, _oct15)], null)));
        await using var context = CreateContext(handler);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/Transactions?from=yesterday");

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));
        Assert.Equal("3M", cut.FindComponent<TransactionLogToolbar>().Instance.SelectedRange);
    }

    [Fact]
    public async Task QueryString_UnknownRange_KeepsTheDefault()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(5, _oct15)], null)));
        await using var context = CreateContext(handler);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/Transactions?range=forever");

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));
        Assert.Equal("3M", cut.FindComponent<TransactionLogToolbar>().Instance.SelectedRange);
    }

    [Fact]
    public async Task FirstPage_RendersRowsGroupedByDay_NewestFirst()
    {
        using var handler = new PageHandler((_, _) => Respond(Page(
            [Entry(5, _oct15), Entry(4, _oct15), Entry(3, _oct14, TransactionKind.Bond)], null)));
        await using var context = CreateContext(handler);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() =>
        {
            var days = cut.FindAll("[data-testid='transactions-day']");
            Assert.Equal(2, days.Count);
            Assert.Contains("15 Oct", days[0].QuerySelector(".mud-card-header")!.TextContent);
            Assert.Equal(2, days[0].QuerySelectorAll("[data-testid='transaction-history-row']").Length);
            Assert.Contains("14 Oct", days[1].QuerySelector(".mud-card-header")!.TextContent);
            Assert.Single(days[1].QuerySelectorAll("[data-testid='transaction-history-row']"));
        });
        var request = Assert.Single(handler.Requests);
        var query = QueryHelpers.ParseQuery(request.Query);
        Assert.Equal("/api/TransactionLog/ItemsPage/7", request.AbsolutePath);
        Assert.Equal(TransactionsPage.PageSize.ToString(CultureInfo.InvariantCulture), query["pageSize"].ToString());
        Assert.False(query.ContainsKey("cursorDate"));
        Assert.Empty(cut.FindAll("[data-testid='transactions-load-more']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='transactions-end']"));
    }

    [Fact]
    public async Task Rows_KeepTheirEntryIds()
    {
        using var handler = new PageHandler((_, _) => Respond(Page(
            [Entry(5, _oct15, TransactionKind.Cash, accountId: 11), Entry(4, _oct15, TransactionKind.Investment, accountId: 12)], null)));
        await using var context = CreateContext(handler);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() =>
        {
            var entryIds = cut.FindComponents<TransactionHistoryRow>().Select(row => row.Instance.Item.EntryId).ToList();
            Assert.Equal([5L, 4L], entryIds);
        });
    }

    [Fact]
    public async Task LoadMore_AppendsNextPageUsingCursor_WithoutDuplicates()
    {
        var cursor = new TransactionLogCursor(_oct14, TransactionKind.Cash, 3);
        using var handler = new PageHandler((count, _) => Respond(count == 1
            ? Page([Entry(5, _oct15), Entry(4, _oct15), Entry(3, _oct14)], cursor)
            : Page([Entry(2, _oct14), Entry(1, _oct13)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid='transaction-history-row']").Count));

        cut.Find("[data-testid='transactions-load-more']").Click();

        cut.WaitForAssertion(() =>
        {
            var entryIds = cut.FindComponents<TransactionHistoryRow>().Select(row => row.Instance.Item.EntryId).ToList();
            Assert.Equal(5, entryIds.Count);
            Assert.Equal(entryIds.Count, entryIds.Distinct().Count());
            // Oct 14 continues across the page boundary: still a single day group.
            Assert.Equal(3, cut.FindAll("[data-testid='transactions-day']").Count);
            Assert.Empty(cut.FindAll("[data-testid='transactions-load-more']"));
        });
        var query = QueryHelpers.ParseQuery(handler.Requests[1].Query);
        Assert.Equal(cursor.Date, ParseDate(query["cursorDate"]!));
        Assert.Equal("Cash", query["cursorKind"].ToString());
        Assert.Equal("3", query["cursorEntryId"].ToString());
        Assert.Equal(handler.Requests[0].Query.Split('&')[0], handler.Requests[1].Query.Split('&')[0]);
    }

    [Fact]
    public async Task ChangingRange_ResetsPagingAndRequestsFromTheStart()
    {
        var cursor = new TransactionLogCursor(_oct14, TransactionKind.Cash, 3);
        using var handler = new PageHandler((count, _) => Respond(count switch
        {
            1 => Page([Entry(5, _oct15), Entry(3, _oct14)], cursor),
            2 => Page([Entry(2, _oct14)], cursor),
            _ => Page([Entry(9, _oct13)], null),
        }));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='transaction-history-row']").Count));
        cut.Find("[data-testid='transactions-load-more']").Click();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid='transaction-history-row']").Count));

        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.SelectedRangeChanged.InvokeAsync("1M"));

        cut.WaitForAssertion(() =>
        {
            var entryIds = cut.FindComponents<TransactionHistoryRow>().Select(row => row.Instance.Item.EntryId).ToList();
            Assert.Equal([9L], entryIds);
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
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct13)], null)));
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
            : Respond(Page([Entry(9, _oct13)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(handler.Requests));

        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.SelectedRangeChanged.InvokeAsync("1M"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));

        firstResponse.SetResult(JsonResponse(Page([Entry(1, _oct15), Entry(2, _oct15)], null)));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        var entryIds = cut.FindComponents<TransactionHistoryRow>().Select(row => row.Instance.Item.EntryId).ToList();
        Assert.Equal([9L], entryIds);
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
            Assert.Empty(cut.FindAll("[data-testid='transaction-history-row']"));
        });
        pending.SetResult(JsonResponse(Page([Entry(1, _oct15)], null)));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='transactions-loading']")));
    }

    [Fact]
    public async Task EmptyRange_ShowsEmptyState()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([], null)));
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
            : Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Couldn't load transactions", cut.Find("[data-testid='transactions-error']").TextContent));

        cut.Find("[data-testid='transactions-error'] button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='transactions-error']"));
            Assert.Single(cut.FindAll("[data-testid='transaction-history-row']"));
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
        var cursor = new TransactionLogCursor(_oct14, TransactionKind.Cash, 3);
        using var handler = new PageHandler((count, _) => count switch
        {
            1 => Respond(Page([Entry(5, _oct15), Entry(3, _oct14)], cursor)),
            2 => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)),
            _ => Respond(Page([Entry(2, _oct14)], null)),
        });
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='transaction-history-row']").Count));

        cut.Find("[data-testid='transactions-load-more']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='transactions-load-more-error']")));
        Assert.Equal(2, cut.FindAll("[data-testid='transaction-history-row']").Count);

        cut.Find("[data-testid='transactions-load-more']").Click();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid='transaction-history-row']").Count));
        Assert.Equal(handler.Requests[1].Query, handler.Requests[2].Query);
    }

    [Fact]
    public async Task OffersTheCurrenciesOfTheUsersOwnTransactions()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)), currencies: ["PLN", "USD"]);
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(["PLN", "USD"], cut.FindComponent<TransactionLogToolbar>().Instance.Currencies));
        Assert.Equal("/api/TransactionLog/Currencies/7", Assert.Single(handler.CurrencyRequests).AbsolutePath);
        Assert.DoesNotContain(handler.Requests, request => request.AbsolutePath.Contains("Currencies"));
    }

    [Fact]
    public async Task HidesTheCurrencyFilter_WhenTheUserHasOneCurrencyOnly()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)), currencies: ["PLN"]);
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));
        cut.WaitForAssertion(() => Assert.Equal(["PLN"], cut.FindComponent<TransactionLogToolbar>().Instance.Currencies));
        Assert.Empty(cut.FindAll("[data-testid='transactions-currency-filter']"));
    }

    [Fact]
    public async Task ChangingTheFilter_ResetsPagingAndSendsTheFilters()
    {
        var cursor = new TransactionLogCursor(_oct14, TransactionKind.Cash, 3);
        using var handler = new PageHandler((count, _) => Respond(count switch
        {
            1 => Page([Entry(5, _oct15), Entry(3, _oct14)], cursor),
            2 => Page([Entry(2, _oct14)], cursor),
            _ => Page([Entry(9, _oct13, TransactionKind.Bond, accountId: 20)], null),
        }));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='transaction-history-row']").Count));
        cut.Find("[data-testid='transactions-load-more']").Click();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid='transaction-history-row']").Count));
        var firstQuery = QueryHelpers.ParseQuery(handler.Requests[0].Query);
        Assert.False(firstQuery.ContainsKey("kinds"));
        Assert.False(firstQuery.ContainsKey("currencies"));

        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.FilterChanged.InvokeAsync(new([TransactionKind.Bond], ["USD"])));

        cut.WaitForAssertion(() =>
        {
            var entryIds = cut.FindComponents<TransactionHistoryRow>().Select(row => row.Instance.Item.EntryId).ToList();
            Assert.Equal([9L], entryIds);
        });
        Assert.Equal(3, handler.Requests.Count);
        var filtered = QueryHelpers.ParseQuery(handler.Requests[2].Query);
        Assert.False(filtered.ContainsKey("cursorDate"));
        Assert.Equal(["Bond"], filtered["kinds"].ToArray());
        Assert.Equal(["USD"], filtered["currencies"].ToArray());
        Assert.Equal(firstQuery["start"], filtered["start"]);
    }

    [Fact]
    public async Task ClickingATypeChip_ReloadsFromTheFirstPageWithThatType()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(handler.Requests));

        cut.FindAll(".fm-txlog-type-chip").Single(chip => chip.TextContent.Contains("Investments")).Click();

        cut.WaitForAssertion(() => Assert.Equal(2, handler.Requests.Count));
        Assert.Equal(["Investment"], QueryHelpers.ParseQuery(handler.Requests[1].Query)["kinds"].ToArray());
    }

    [Fact]
    public async Task LaterFilterChange_WinsOverASlowerEarlierResponse()
    {
        var firstResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new PageHandler((count, _) => count == 1
            ? firstResponse.Task
            : Respond(Page([Entry(9, _oct13, TransactionKind.Bond, accountId: 20)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(handler.Requests));

        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.FilterChanged.InvokeAsync(new([TransactionKind.Bond], [])));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));
        firstResponse.SetResult(JsonResponse(Page([Entry(1, _oct15), Entry(2, _oct15)], null)));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal([9L], cut.FindComponents<TransactionHistoryRow>().Select(row => row.Instance.Item.EntryId));
    }

    [Fact]
    public async Task EmptyResult_WithActiveFilters_SaysNoTransactionsMatchTheFilters()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Contains("No transactions between", cut.Find("[data-testid='transactions-empty']").TextContent));
        Assert.DoesNotContain("filters", cut.Find("[data-testid='transactions-empty']").TextContent);

        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.FilterChanged.InvokeAsync(new([], ["PLN"])));

        cut.WaitForAssertion(() => Assert.Contains("No transactions match the selected filters",
            cut.Find("[data-testid='transactions-empty']").TextContent));
    }

    [Fact]
    public async Task FailingToLoadCurrencies_StillLoadsTheTransactions()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));
        Assert.Empty(cut.FindComponent<TransactionLogToolbar>().Instance.Currencies);
        Assert.Empty(cut.FindAll("[data-testid='transactions-error']"));
    }

    [Fact]
    public async Task SuccessfulAccountEdit_ReloadsFromTheFirstPage_KeepingTheFilters()
    {
        var cursor = new TransactionLogCursor(_oct14, TransactionKind.Cash, 3);
        using var handler = new PageHandler((count, _) => Respond(count == 3
            ? Page([Entry(8, _oct15)], null)
            : Page([Entry(5, _oct15), Entry(3, _oct14)], cursor)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='transaction-history-row']").Count));
        await cut.InvokeAsync(() => cut.FindComponent<TransactionLogToolbar>().Instance.SearchTextChanged.InvokeAsync("rent"));
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Requests.Count));

        // What a cash or bond row publishes after a successful save or delete.
        await context.Services.GetRequiredService<AccountDataSynchronizationService>().AccountChanged();

        cut.WaitForAssertion(() => Assert.Equal([8L],
            cut.FindComponents<TransactionHistoryRow>().Select(row => row.Instance.Item.EntryId)));
        var reload = QueryHelpers.ParseQuery(handler.Requests[2].Query);
        Assert.False(reload.ContainsKey("cursorDate"));
        Assert.Equal("rent", reload["search"].ToString());
    }

    [Fact]
    public async Task AfterDisposal_AccountChangesNoLongerReload()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(handler.Requests));

        await context.DisposeComponentsAsync();
        await context.Services.GetRequiredService<AccountDataSynchronizationService>().AccountChanged();

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task InvestmentRows_GetTheirValuation_InOneRequestCoveringLinkedAndUnlinkedPurchases()
    {
        using var handler = new PageHandler((_, _) => Respond(Page(
            [Entry(3, _oct15, TransactionKind.Investment, accountId: 30), Entry(4, _oct15, TransactionKind.Investment, accountId: null)], null)));
        using var services = new PageHandler((_, uri) => Task.FromResult(uri.AbsolutePath.Contains("TransactionValuations")
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<IReadOnlyList<InvestmentTransactionValuationDto>>(
                    [new(3, 10m, 20m, 12m, 24m, 4m, 20m, "PLN", false, true), new(4, 10m, 20m, 13m, 26m, 6m, 30m, "PLN", false, true)])
            }
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var context = CreateContext(handler, services);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Equal(26m,
            cut.FindComponents<InvestmentTransactionRow>().Single(row => row.Instance.Transaction.Id == 4).Instance.Valuation?.CurrentValuation));
        Assert.Equal(24m, cut.FindComponents<InvestmentTransactionRow>().Single(row => row.Instance.Transaction.Id == 3).Instance.Valuation?.CurrentValuation);
        // One user-scoped request values every visible purchase, whether or not it is linked to an account.
        var request = Assert.Single(services.Requests);
        Assert.Equal("/api/InvestmentValuation/TransactionValuations/0", request.AbsolutePath);
        Assert.Equal(["3", "4"], QueryHelpers.ParseQuery(request.Query)["transactionIds"].Order().Select(id => id!));
    }

    [Fact]
    public async Task InvestmentRows_StayOnTheirFallbacks_WhenTheValuationRequestTimesOut()
    {
        using var handler = new PageHandler((_, _) => Respond(Page(
            [Entry(3, _oct15, TransactionKind.Investment, accountId: null)], null)));
        using var services = new PageHandler((_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")));
        await using var context = CreateContext(handler, services);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Single(services.Requests));
        Assert.Null(cut.FindComponent<InvestmentTransactionRow>().Instance.Valuation);
    }

    [Fact]
    public async Task DeletingAnInvestmentRow_UsesTheOwnerRoute_AndReloads()
    {
        using var handler = new PageHandler((count, _) => Respond(count == 1
            ? Page([Entry(3, _oct15, TransactionKind.Investment, accountId: 30)], null)
            : Page([], null)));
        using var services = new PageHandler((_, uri) => Task.FromResult(new HttpResponseMessage(
            uri.AbsolutePath.StartsWith("/api/transactions/investment/") ? HttpStatusCode.NoContent : HttpStatusCode.ServiceUnavailable)));
        await using var context = CreateContext(handler, services);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<InvestmentTransactionRow>()));

        var row = cut.FindComponent<InvestmentTransactionRow>();
        await cut.InvokeAsync(() => row.Instance.OnDelete.InvokeAsync(row.Instance.Transaction));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='transactions-empty']")));
        Assert.Contains(services.Requests, uri => uri.AbsolutePath == "/api/transactions/investment/3");
        Assert.Equal(2, handler.Requests.Count);
        // Deleting the last trade of a currency must drop it from the filter options.
        cut.WaitForAssertion(() => Assert.Equal(2, handler.CurrencyRequests.Count));
    }

    [Fact]
    public async Task DeleteFinishingAfterLeavingThePage_DoesNotReloadOrFail()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(3, _oct15, TransactionKind.Investment, accountId: 30)], null)));
        var deleted = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var services = new PageHandler((_, uri) => uri.AbsolutePath.StartsWith("/api/transactions/investment/")
            ? deleted.Task
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var context = CreateContext(handler, services);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<InvestmentTransactionRow>()));
        var row = cut.FindComponent<InvestmentTransactionRow>();
        var transaction = row.Instance.Transaction;
        var onDelete = row.Instance.OnDelete;

        var deleting = cut.InvokeAsync(() => onDelete.InvokeAsync(transaction));
        cut.WaitForAssertion(() => Assert.Contains(services.Requests, uri => uri.AbsolutePath.StartsWith("/api/transactions/investment/")));
        await context.DisposeComponentsAsync();
        deleted.SetResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        await deleting;

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task UnlinkedRowsExpandWithOwnerCommandsAndWithoutRunningValues()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([
            Entry(1, _oct15, accountId: null), Entry(2, _oct15, TransactionKind.Bond, null),
            Entry(3, _oct15, TransactionKind.Investment, null)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindComponents<TransactionHistoryRow>().Count));
        foreach (var row in cut.FindAll("[data-testid='transaction-history-row'] [role=button][tabindex='0']"))
            row.Click();
        Assert.Contains("Units change", cut.Markup);
        Assert.DoesNotContain("Running balance", cut.Markup);
        Assert.DoesNotContain("position ", cut.Markup, StringComparison.OrdinalIgnoreCase);
        var actions = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToList();
        Assert.Equal(3, actions.Count(action => action == "Edit"));
        Assert.Equal(3, actions.Count(action => action == "Delete"));
        Assert.DoesNotContain("Remove", actions);
    }

    [Fact]
    public async Task HeaderOffersAddAndImportExport()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='transactions-add']")));
        Assert.NotEmpty(cut.FindAll("[data-testid='transactions-import-export']"));
    }

    [Fact]
    public async Task Add_OpensTheDialogOnCash()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='transactions-add']")));
        var dialog = cut.FindComponent<AddTransactionDialog>();
        Assert.False(dialog.Instance.Visible);

        cut.Find("[data-testid='transactions-add']").Click();

        Assert.True(dialog.Instance.Visible);
        Assert.Equal(TransactionKind.Cash, dialog.Instance.InitialKind);
    }

    [Fact]
    public async Task Add_OpensOnTheSingleKindTheListIsFilteredTo()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='transaction-history-row']")));
        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.FilterChanged.InvokeAsync(new([TransactionKind.Bond], [])));

        cut.Find("[data-testid='transactions-add']").Click();

        Assert.Equal(TransactionKind.Bond, cut.FindComponent<AddTransactionDialog>().Instance.InitialKind);
    }

    [Fact]
    public async Task DialogQuery_OpensTheDialogOnTheRequestedKind_WithoutFiltering()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/Transactions?dialog=add-entry&kind=bond");

        var cut = context.Render<TransactionsPage>();

        var dialog = cut.FindComponent<AddTransactionDialog>();
        Assert.True(dialog.Instance.Visible);
        Assert.Equal(TransactionKind.Bond, dialog.Instance.InitialKind);
        cut.WaitForAssertion(() => Assert.False(QueryHelpers.ParseQuery(handler.Requests.Last().Query).ContainsKey("kinds")));
    }

    [Fact]
    public async Task InvestmentSavedInTheAddDialog_ReloadsTheList()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(handler.Requests));

        await cut.InvokeAsync(() => cut.FindComponent<AddTransactionDialog>().Instance.OnSaved.InvokeAsync());

        cut.WaitForAssertion(() => Assert.Equal(2, handler.Requests.Count));
        // A first trade in a new currency must reach the filter options too.
        cut.WaitForAssertion(() => Assert.Equal(2, handler.CurrencyRequests.Count));
    }

    [Fact]
    public async Task DialogQuery_WithAnUndefinedKind_OpensOnCash()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        await using var context = CreateContext(handler);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/Transactions?dialog=add-entry&kind=999");

        var cut = context.Render<TransactionsPage>();

        Assert.Equal(TransactionKind.Cash, cut.FindComponent<AddTransactionDialog>().Instance.InitialKind);
    }

    [Fact]
    public async Task EmptyList_OffersImportAndAdd_AndClearFiltersOnlyWhenFiltered()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([], null)));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='transactions-empty']")));
        Assert.Contains(cut.FindAll("[data-testid='transactions-empty'] a"), link => link.GetAttribute("href") == "/Transactions/Import/cash");
        Assert.Contains(cut.FindAll("[data-testid='transactions-empty'] button"), button => button.TextContent.Contains("Add transaction"));
        Assert.Empty(cut.FindAll("[data-testid='transactions-clear-filters']"));

        var toolbar = cut.FindComponent<TransactionLogToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.FilterChanged.InvokeAsync(new([TransactionKind.Cash], [])));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='transactions-clear-filters']")));
        // The header already offers Import and Add, so a filtered view shows only the way out of the filters.
        Assert.Empty(cut.FindAll("[data-testid='transactions-empty'] a"));
        Assert.Single(cut.FindAll("[data-testid='transactions-empty'] button"));
        cut.Find("[data-testid='transactions-clear-filters']").Click();

        cut.WaitForAssertion(() => Assert.False(toolbar.Instance.Filter.IsActive));
        Assert.Empty(cut.FindAll("[data-testid='transactions-clear-filters']"));
    }

    private static TransactionLogItemDto Entry(long id, DateTime date, TransactionKind type = TransactionKind.Cash, int? accountId = 10)
    {
        var valueChange = id % 2 == 0 ? 25m : -12.5m;
        return type switch
        {
            TransactionKind.Bond => new BondTransactionLogItemDto(accountId, id, date, valueChange, $"Row {id}", 1, "PLN", null, []),
            TransactionKind.Investment => new InvestmentTransactionLogItemDto(accountId, new InvestmentTransactionDto(
                id, 7, accountId, 1, InvestmentTransactionTypeDto.Buy, 2m, 10m, "PLN", DateOnly.FromDateTime(date), null, $"Row {id}", "TCK")),
            _ => new CurrencyTransactionLogItemDto(accountId, id, date, valueChange, $"Row {id}", 1, "PLN", null, []),
        };
    }

    private static TransactionLogItemsPageDto Page(IReadOnlyList<TransactionLogItemDto> items, TransactionLogCursor? cursor) =>
        new(items, cursor is null ? null : new(cursor.Date, (TransactionKindDto)cursor.Kind, cursor.EntryId));

    private static DateTime ParseDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static HttpResponseMessage JsonResponse(TransactionLogItemsPageDto page) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(page) };

    private static Task<HttpResponseMessage> Respond(TransactionLogItemsPageDto page) => Task.FromResult(JsonResponse(page));

    private static BunitContext CreateContext(HttpMessageHandler handler, HttpMessageHandler? services = null)
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

        // Row enrichment (bond definitions, valuations) and the rows' own services. Enrichment requests fail,
        // which the page tolerates, so the paged-feed handler only ever sees feed requests.
        var unavailable = new HttpClient(services ?? new UnavailableHandler()) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new InvestmentValuationHttpClient(unavailable));
        context.Services.AddSingleton(new InvestmentTransactionHttpClient(unavailable));
        context.Services.AddSingleton(new InvestmentTradesHttpClient(unavailable));
        context.Services.AddSingleton(new CashTransactionsHttpClient(unavailable));
        context.Services.AddSingleton(new BondTransactionsHttpClient(unavailable));
        // What the add dialog's forms load when they open.
        var forms = RecordingHttpHandler.ForEntryForms().CreateClient();
        context.Services.AddSingleton(new CurrencyHttpClient(forms));
        context.Services.AddSingleton(new BondDetailsHttpClient(forms));
        context.Services.AddSingleton(new FinanceManager.Components.Features.Labels.HttpClients.FinancialLabelHttpClient(forms));
        context.Services.AddSingleton<AccountDataSynchronizationService>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        return context;
    }

    private sealed class UnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    // Feed requests go to `respond`; the currency-options request is answered from `currencies` (unavailable when null)
    // and recorded apart, so request counts and indexes only ever describe the feed.
    private sealed class PageHandler(Func<int, Uri, Task<HttpResponseMessage>> respond, IReadOnlyList<string>? currencies = null) : HttpMessageHandler
    {
        private readonly List<Uri> _requests = [];
        private readonly List<Uri> _currencyRequests = [];

        public IReadOnlyList<Uri> CurrencyRequests
        {
            get
            {
                lock (_requests)
                    return [.. _currencyRequests];
            }
        }

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
                if (request.RequestUri!.AbsolutePath.Contains("/Currencies/", StringComparison.Ordinal))
                {
                    _currencyRequests.Add(request.RequestUri);
                    return Task.FromResult(currencies is null
                        ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                        : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(currencies) });
                }
                _requests.Add(request.RequestUri!);
                count = _requests.Count;
            }
            return respond(count, request.RequestUri!);
        }
    }
}