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
    public async Task GetItemsPage_EncodesSearchOrOmitsBlankSearch(string? search)
    {
        using var handler = new StubHandler(request =>
        {
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            if (string.IsNullOrWhiteSpace(search)) Assert.False(query.ContainsKey("search"));
            else Assert.Equal(search, query["search"].ToString());
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new TransactionLogItemsPageDto([], null)) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        await new TransactionLogHttpClient(http).GetItemsPage(7, new(DateTime.MinValue, DateTime.MaxValue, Search: search), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetItemsPage_EncodesRangeAndCompositeCursor_AndReadsPage()
    {
        var start = new DateTime(2024, 3, 1, 12, 30, 0, DateTimeKind.Utc);
        var cursor = new TransactionLogCursor(start.AddDays(1), TransactionKind.Bond, long.MaxValue);
        var page = new TransactionLogItemsPageDto([], new(cursor.Date, TransactionKindDto.Bond, cursor.EntryId));
        using var handler = new StubHandler(request =>
        {
            Assert.Equal("/api/TransactionLog/ItemsPage/7", request.RequestUri!.AbsolutePath);
            var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
            Assert.Equal(start.ToString("O", CultureInfo.InvariantCulture), query["start"].ToString());
            Assert.Equal(start.AddDays(2).ToString("O", CultureInfo.InvariantCulture), query["end"].ToString());
            Assert.Equal("2", query["pageSize"].ToString());
            Assert.Equal(cursor.Date.ToString("O", CultureInfo.InvariantCulture), query["cursorDate"].ToString());
            Assert.Equal("Bond", query["cursorKind"].ToString());
            Assert.Equal("9223372036854775807", query["cursorEntryId"].ToString());
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(page) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TransactionLogHttpClient(http);

        var result = await client.GetItemsPage(7, new(start, start.AddDays(2), 2, cursor), TestContext.Current.CancellationToken);

        Assert.Empty(result.Items);
        Assert.Equal(cursor, result.NextCursor);
    }

    [Fact]
    public async Task GetItemsPage_ReadsEveryEntryKind_AsTheMatchingHistoryItem()
    {
        var date = new DateTime(2024, 3, 1, 12, 30, 0, DateTimeKind.Utc);
        var label = new FinancialLabelDto { Id = 4, Name = "Food" };
        var page = new TransactionLogItemsPageDto(
        [
            new CurrencyTransactionLogItemDto(10, 1, date, -10, "Lunch", 3, "EUR", null, [label]),
            new CurrencyTransactionLogItemDto(null, 2, date, -10, "Dinner", 3, "EUR", null, []),
            new BondTransactionLogItemDto(20, 1, date, 5, "", 7, "EUR", "Treasury", [label]),
            new InvestmentTransactionLogItemDto(30, new InvestmentTransactionDto(
                long.MaxValue, 7, 30, 1, InvestmentTransactionTypeDto.Sell, 1, 2, "USD", new DateOnly(2024, 3, 1), null, null, Labels: [label])),
        ], new TransactionLogCursorDto(date, TransactionKindDto.Investment, long.MaxValue));
        using var handler = new StubHandler(request =>
        {
            Assert.Equal("/api/TransactionLog/ItemsPage/7", request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(page) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var result = await new TransactionLogHttpClient(http).GetItemsPage(7, new(date, date), TestContext.Current.CancellationToken);

        Assert.Equal(new TransactionLogCursor(date, TransactionKind.Investment, long.MaxValue), result.NextCursor);
        Assert.Equal([TransactionKind.Cash, TransactionKind.Cash, TransactionKind.Bond, TransactionKind.Investment], result.Items.Select(i => i.Kind));
        var cash = Assert.IsType<CurrencyTransactionHistoryItem>(result.Items[0]);
        Assert.Equal((7, 10, "Cash EUR", 3), (cash.Account!.UserId, cash.Account.AccountId, cash.Account.Name, cash.Account.CurrencyId));
        Assert.Equal(("EUR", "Lunch", -10m, "Food"), (cash.Currency, cash.Entry.Description, cash.Entry.ValueChange, cash.Entry.Labels.Single().Name));
        // Owned entries without a legacy link have no command context.
        Assert.Null(Assert.IsType<CurrencyTransactionHistoryItem>(result.Items[1]).Account);
        var bond = Assert.IsType<BondTransactionHistoryItem>(result.Items[2]);
        Assert.Equal((20, "Treasury", 7, "Food"), (bond.Account!.AccountId, bond.Account.Name, bond.Entry.BondDetailsId, bond.Entry.Labels.Single().Name));
        var trade = Assert.IsType<InvestmentTransactionHistoryItem>(result.Items[3]);
        Assert.Equal((long.MaxValue, "Food"), (trade.EntryId, trade.Transaction.Labels!.Single().Name));
        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), trade.Date);
    }

    [Fact]
    public async Task GetItemsPage_WithFilters_RepeatsKindsAndCurrencies()
    {
        var start = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        string? rawQuery = null;
        using var handler = new StubHandler(request =>
        {
            rawQuery = request.RequestUri!.Query;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new TransactionLogItemsPageDto([], null)) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TransactionLogHttpClient(http);

        await client.GetItemsPage(7, new(start, start.AddDays(2), 25, null,
            [TransactionKind.Bond, TransactionKind.Investment], ["PLN", "GBX"]), TestContext.Current.CancellationToken);

        var query = QueryHelpers.ParseQuery(rawQuery);
        Assert.Equal(["Bond", "Investment"], query["kinds"].ToArray());
        Assert.Equal(["PLN", "GBX"], query["currencies"].ToArray());
    }

    [Fact]
    public async Task GetItemsPage_WithoutFilters_OmitsFilterParameters()
    {
        string? rawQuery = null;
        using var handler = new StubHandler(request =>
        {
            rawQuery = request.RequestUri!.Query;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new TransactionLogItemsPageDto([], null)) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TransactionLogHttpClient(http);

        await client.GetItemsPage(7, new(DateTime.MinValue, DateTime.MaxValue, Kinds: [], Currencies: []), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("kinds", rawQuery);
        Assert.DoesNotContain("currencies", rawQuery);
    }

    [Fact]
    public async Task GetItemsPage_FirstPageOmitsCursor_AndPropagatesHttpFailure()
    {
        using var handler = new StubHandler(request =>
        {
            Assert.DoesNotContain("cursor", request.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
            return new(HttpStatusCode.Forbidden);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TransactionLogHttpClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetItemsPage(7,
            new(DateTime.MinValue, DateTime.MaxValue), TestContext.Current.CancellationToken));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}