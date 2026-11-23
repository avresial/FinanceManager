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
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.FinancialAccounts.Bond;
using FinanceManager.Contracts.FinancialAccounts.Currencies;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Contracts.FinancialAccounts.Shared;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
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
    public async Task LoadsSortedCurrencyCodesAndValidMinorQuoteUnits()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(1, _oct15)], null)));
        using var catalog = new PageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new[] { DefaultCurrency.USD, DefaultCurrency.PLN }) }));
        await using var context = CreateContext(handler, out _, catalog: catalog);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Equal(["GBX", "ILA", "PLN", "USD", "ZAC"], cut.FindComponent<TransactionLogToolbar>().Instance.Currencies));
        Assert.Single(catalog.Requests);
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
        await using var context = CreateContext(handler, out var accountService);

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
    public async Task InvestmentRows_GetTheirValuation_RequestedPerAccountForPurchases()
    {
        using var handler = new PageHandler((_, _) => Respond(Page(
            [Entry(3, _oct15, TransactionKind.Investment, accountId: 30), Entry(4, _oct15, TransactionKind.Investment, accountId: 31)], null)));
        // Account 31's valuation request times out; account 30's valuation must still reach its row.
        using var services = new PageHandler((_, uri) => uri.AbsolutePath.Contains("/31/")
            ? Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout"))
            : Task.FromResult(uri.AbsolutePath.Contains("TransactionValuations")
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create<IReadOnlyList<InvestmentTransactionValuationDto>>(
                        [new(3, 10m, 20m, 12m, 24m, 4m, 20m, "PLN", false, true)])
                }
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var context = CreateContext(handler, out _, services);

        var cut = context.Render<TransactionsPage>();

        cut.WaitForAssertion(() => Assert.Equal(24m,
            cut.FindComponents<InvestmentTransactionRow>().Single(row => row.Instance.Transaction.Id == 3).Instance.Valuation?.CurrentValuation));
        Assert.Null(cut.FindComponents<InvestmentTransactionRow>().Single(row => row.Instance.Transaction.Id == 4).Instance.Valuation);
        Assert.Equal(["/api/InvestmentValuation/TransactionValuations/30/0", "/api/InvestmentValuation/TransactionValuations/31/0"],
            services.Requests.Select(uri => uri.AbsolutePath).Order());
        Assert.Equal("?transactionIds=3", services.Requests.Single(uri => uri.AbsolutePath.EndsWith("/30/0")).Query);
    }

    [Fact]
    public async Task DeletingAnInvestmentRow_UsesItsOwnAccount_AndReloads()
    {
        using var handler = new PageHandler((count, _) => Respond(count == 1
            ? Page([Entry(3, _oct15, TransactionKind.Investment, accountId: 30)], null)
            : Page([], null)));
        using var services = new PageHandler((_, uri) => Task.FromResult(new HttpResponseMessage(
            uri.AbsolutePath.Contains("/Delete/") ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable)));
        await using var context = CreateContext(handler, out _, services);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<InvestmentTransactionRow>()));

        var row = cut.FindComponent<InvestmentTransactionRow>();
        await cut.InvokeAsync(() => row.Instance.OnDelete.InvokeAsync(row.Instance.Transaction));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='transactions-empty']")));
        Assert.Contains(services.Requests, uri => uri.AbsolutePath == "/api/InvestmentTransaction/Delete/30/3");
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DeleteFinishingAfterLeavingThePage_DoesNotReloadOrFail()
    {
        using var handler = new PageHandler((_, _) => Respond(Page([Entry(3, _oct15, TransactionKind.Investment, accountId: 30)], null)));
        var deleted = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var services = new PageHandler((_, uri) => uri.AbsolutePath.Contains("/Delete/")
            ? deleted.Task
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var context = CreateContext(handler, out _, services);
        var cut = context.Render<TransactionsPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<InvestmentTransactionRow>()));
        var row = cut.FindComponent<InvestmentTransactionRow>();
        var transaction = row.Instance.Transaction;
        var onDelete = row.Instance.OnDelete;

        var deleting = cut.InvokeAsync(() => onDelete.InvokeAsync(transaction));
        cut.WaitForAssertion(() => Assert.Contains(services.Requests, uri => uri.AbsolutePath.Contains("/Delete/")));
        await context.DisposeComponentsAsync();
        deleted.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await deleting;

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task UnlinkedRowsExpandWithoutAccountCommandsOrRunningValues()
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
        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Trim() is "Edit" or "Remove" or "Delete");
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

    private static BunitContext CreateContext(HttpMessageHandler handler) => CreateContext(handler, out _);

    private static BunitContext CreateContext(HttpMessageHandler handler, out Mock<IFinancialAccountService> accountService,
        HttpMessageHandler? services = null, HttpMessageHandler? catalog = null)
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
        accountService = new Mock<IFinancialAccountService>();
        context.Services.AddSingleton(new CurrencyHttpClient(new HttpClient(catalog ?? new UnavailableHandler()) { BaseAddress = new Uri("http://localhost/") }));
        context.Services.AddSingleton(accountService.Object);
        context.Services.AddSingleton(new TransactionLogHttpClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));

        // Row enrichment (bond definitions, valuations) and the rows' own services. Enrichment requests fail,
        // which the page tolerates, so the paged-feed handler only ever sees feed requests.
        var unavailable = new HttpClient(services ?? new UnavailableHandler()) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new BondDetailsHttpClient(unavailable));
        context.Services.AddSingleton(new InvestmentValuationHttpClient(unavailable));
        context.Services.AddSingleton(new InvestmentTransactionHttpClient(unavailable));
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