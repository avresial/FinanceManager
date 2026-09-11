using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.HttpClients;

/// <summary>
/// The investment account-details page paints from a stored snapshot and reconciles against these
/// two clients, so "no data" and "the request failed" must never look the same: an empty list
/// returned for a failure would clear the trade list and overwrite the snapshot with nothing.
/// </summary>
[Trait("Category", "Unit")]
public class InvestmentAccountDetailsHttpClientTests
{
    [Fact]
    public async Task GetByAccountAsync_FailedRequest_Throws()
    {
        var client = new InvestmentTransactionHttpClient(CreateHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetByAccountAsync(1));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GetByAccountAsync_AccountWithoutTrades_ReturnsEmptyList(HttpStatusCode statusCode)
    {
        var client = new InvestmentTransactionHttpClient(CreateHttpClient(new HttpResponseMessage(statusCode)));

        Assert.Empty(await client.GetByAccountAsync(1));
    }

    [Fact]
    public async Task GetHistoryPageAsync_SendsCursorAndServerFilters()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new InvestmentTransactionHistoryPageDto([], true, "next"))
        });
        var client = new InvestmentTransactionHttpClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var page = await client.GetHistoryPageAsync(
            7,
            pageSize: 25,
            cursor: "2026-01-01:123",
            startDate: new DateOnly(2025, 1, 1),
            endDate: new DateOnly(2026, 1, 1),
            type: InvestmentTransactionType.Buy,
            search: "CSPX & ETF",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(page.HasMore);
        Assert.Equal("next", page.NextCursor);
        Assert.NotNull(handler.LastRequest);
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("pageSize=25", query);
        Assert.Contains("cursor=2026-01-01%3A123", query);
        Assert.Contains("startDate=2025-01-01", query);
        Assert.Contains("endDate=2026-01-01", query);
        Assert.Contains("type=Buy", query);
        Assert.Contains("search=CSPX%20%26%20ETF", query);
    }

    [Fact]
    public async Task GetHoldingMetadataAsync_SendsAsOfDateAndReadsRows()
    {
        var transaction = new InvestmentTransactionDto(
            17,
            1,
            7,
            42,
            InvestmentTransactionType.Buy,
            2m,
            100m,
            "USD",
            new DateOnly(2026, 1, 10),
            null,
            null,
            "CSPX",
            "NASDAQ",
            FinanceManager.Domain.Assets.Entities.AssetType.ETF);
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[] { transaction })
        });
        var client = new InvestmentTransactionHttpClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var result = await client.GetHoldingMetadataAsync(
            7,
            new DateTime(2026, 2, 1),
            TestContext.Current.CancellationToken);

        Assert.Equal(17, Assert.Single(result).Id);
        Assert.Equal("/api/InvestmentTransaction/GetHoldingMetadata/7/2026-02-01", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetTransactionValuationsAsync_FailedRequest_Throws()
    {
        var client = new InvestmentValuationHttpClient(CreateHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTransactionValuationsAsync(
            1, 1, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetTransactionValuationsAsync_NothingPriced_ReturnsEmptyList()
    {
        var client = new InvestmentValuationHttpClient(CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<object>())
        }));

        Assert.Empty(await client.GetTransactionValuationsAsync(1, 1, cancellationToken: TestContext.Current.CancellationToken));
    }

    private static HttpClient CreateHttpClient(HttpResponseMessage response) =>
        new(new StubHandler(response)) { BaseAddress = new Uri("http://localhost/") };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Capture(request));

        private HttpResponseMessage Capture(HttpRequestMessage request)
        {
            LastRequest = request;
            return response;
        }
    }
}