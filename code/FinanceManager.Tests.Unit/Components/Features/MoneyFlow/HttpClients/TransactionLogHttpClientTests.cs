using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.FinancialAccounts.Bond;
using FinanceManager.Contracts.FinancialAccounts.Currencies;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Contracts.FinancialAccounts.Shared;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.MoneyFlow.HttpClients;

[Trait("Category", "Unit")]
public class TransactionLogHttpClientTests
{
    [Theory]
    [InlineData("Café & bonds?+%_[]")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetPage_EncodesSearchOrOmitsBlankSearch(string? search)
    {
        using var handler = new StubHandler(request =>
        {
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            if (string.IsNullOrWhiteSpace(search)) Assert.False(query.ContainsKey("search"));
            else Assert.Equal(search, query["search"].ToString());
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new TransactionLogPage([], null)) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        await new TransactionLogHttpClient(http).GetPage(7, new(DateTime.MinValue, DateTime.MaxValue, Search: search), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetPage_EncodesRangeAndCompositeCursor_AndReadsPage()
    {
        var start = new DateTime(2024, 3, 1, 12, 30, 0, DateTimeKind.Utc);
        var cursor = new TransactionLogCursor(start.AddDays(1), AccountType.Bond, long.MaxValue);
        var page = new TransactionLogPage([new(10, "Bank", AccountType.Currency, 1, start, 2, "Row")], cursor);
        using var handler = new StubHandler(request =>
        {
            Assert.Equal("/api/TransactionLog/Page/7", request.RequestUri!.AbsolutePath);
            var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
            Assert.Equal(start.ToString("O", CultureInfo.InvariantCulture), query["start"].ToString());
            Assert.Equal(start.AddDays(2).ToString("O", CultureInfo.InvariantCulture), query["end"].ToString());
            Assert.Equal("2", query["pageSize"].ToString());
            Assert.Equal(cursor.Date.ToString("O", CultureInfo.InvariantCulture), query["cursorDate"].ToString());
            Assert.Equal("Bond", query["cursorAccountType"].ToString());
            Assert.Equal("9223372036854775807", query["cursorEntryId"].ToString());
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(page) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TransactionLogHttpClient(http);

        var result = await client.GetPage(7, new(start, start.AddDays(2), 2, cursor), TestContext.Current.CancellationToken);

        Assert.Equal(page.Items, result.Items);
        Assert.Equal(cursor, result.NextCursor);
    }

    [Fact]
    public async Task GetItemsPage_ReadsEveryEntryKind_AsTheMatchingHistoryItem()
    {
        var date = new DateTime(2024, 3, 1, 12, 30, 0, DateTimeKind.Utc);
        var label = new FinancialLabelDto { Id = 4, Name = "Food" };
        var page = new TransactionLogItemsPageDto(
        [
            new CurrencyTransactionLogItemDto(10, "Bank", 3, "EUR", new CurrencyAccountEntryDto
            {
                AccountId = 10, EntryId = 1, PostingDate = date, Value = 90, ValueChange = -10, Description = "Lunch", Labels = [label]
            }),
            new CurrencyTransactionLogItemDto(10, "Bank", 3, "EUR", new CurrencyAccountEntryDto { AccountId = 10, EntryId = 2, PostingDate = date }),
            new BondTransactionLogItemDto(20, "Bonds", new BondAccountEntryDto
            {
                AccountId = 20, EntryId = 1, PostingDate = date, Value = 5, ValueChange = 5, BondDetailsId = 7, Labels = [label]
            }),
            new InvestmentTransactionLogItemDto(30, "Broker", new InvestmentTransactionDto(
                long.MaxValue, 7, 30, 1, InvestmentTransactionTypeDto.Sell, 1, 2, "USD", new DateOnly(2024, 3, 1), null, null, Labels: [label])),
        ], new TransactionLogCursorDto(date, AccountTypeDto.Stock, long.MaxValue));
        using var handler = new StubHandler(request =>
        {
            Assert.Equal("/api/TransactionLog/ItemsPage/7", request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(page) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var result = await new TransactionLogHttpClient(http).GetItemsPage(7, new(date, date), TestContext.Current.CancellationToken);

        Assert.Equal(new TransactionLogCursor(date, AccountType.Stock, long.MaxValue), result.NextCursor);
        Assert.Equal([AccountType.Currency, AccountType.Currency, AccountType.Bond, AccountType.Stock], result.Items.Select(i => i.AccountType));
        var cash = Assert.IsType<CurrencyTransactionHistoryItem>(result.Items[0]);
        Assert.Equal((7, 10, "Bank", 3), (cash.Account.UserId, cash.Account.AccountId, cash.Account.Name, cash.Account.CurrencyId));
        Assert.Equal(("EUR", "Lunch", -10m, "Food"), (cash.Currency, cash.Entry.Description, cash.Entry.ValueChange, cash.Entry.Labels.Single().Name));
        // Rows of one account share its lightweight account instance.
        Assert.Same(cash.Account, Assert.IsType<CurrencyTransactionHistoryItem>(result.Items[1]).Account);
        var bond = Assert.IsType<BondTransactionHistoryItem>(result.Items[2]);
        Assert.Equal((20, "Bonds", 7, "Food"), (bond.Account.AccountId, bond.Account.Name, bond.Entry.BondDetailsId, bond.Entry.Labels.Single().Name));
        var trade = Assert.IsType<InvestmentTransactionHistoryItem>(result.Items[3]);
        Assert.Equal((long.MaxValue, "Food"), (trade.EntryId, trade.Transaction.Labels!.Single().Name));
        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), trade.Date);
    }

    [Fact]
    public async Task GetPage_WithFilters_RepeatsAccountTypesAndAccountIds()
    {
        var start = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        string? rawQuery = null;
        using var handler = new StubHandler(request =>
        {
            rawQuery = request.RequestUri!.Query;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new TransactionLogPage([], null)) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TransactionLogHttpClient(http);

        await client.GetPage(7, new(start, start.AddDays(2), 25, null,
            [AccountType.Bond, AccountType.Stock], [4, 1234567]), TestContext.Current.CancellationToken);

        var query = QueryHelpers.ParseQuery(rawQuery);
        Assert.Equal(["Bond", "Stock"], query["accountTypes"].ToArray());
        Assert.Equal(["4", "1234567"], query["accountIds"].ToArray());
    }

    [Fact]
    public async Task GetPage_WithoutFilters_OmitsFilterParameters()
    {
        string? rawQuery = null;
        using var handler = new StubHandler(request =>
        {
            rawQuery = request.RequestUri!.Query;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new TransactionLogPage([], null)) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TransactionLogHttpClient(http);

        await client.GetPage(7, new(DateTime.MinValue, DateTime.MaxValue, AccountTypes: [], AccountIds: []), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("accountTypes", rawQuery);
        Assert.DoesNotContain("accountIds", rawQuery);
    }

    [Fact]
    public async Task GetPage_FirstPageOmitsCursor_AndPropagatesHttpFailure()
    {
        using var handler = new StubHandler(request =>
        {
            Assert.DoesNotContain("cursor", request.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
            return new(HttpStatusCode.Forbidden);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TransactionLogHttpClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetPage(7,
            new(DateTime.MinValue, DateTime.MaxValue), TestContext.Current.CancellationToken));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}