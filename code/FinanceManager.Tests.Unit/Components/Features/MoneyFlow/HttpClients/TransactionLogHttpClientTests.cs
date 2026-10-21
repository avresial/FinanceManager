using FinanceManager.Components.Features.MoneyFlow.HttpClients;
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