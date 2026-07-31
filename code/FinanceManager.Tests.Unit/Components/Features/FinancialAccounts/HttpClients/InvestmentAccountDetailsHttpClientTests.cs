using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
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
    public async Task GetTransactionValuationsAsync_FailedRequest_Throws()
    {
        var client = new InvestmentValuationHttpClient(CreateHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTransactionValuationsAsync(1, 1));
    }

    [Fact]
    public async Task GetTransactionValuationsAsync_NothingPriced_ReturnsEmptyList()
    {
        var client = new InvestmentValuationHttpClient(CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<object>())
        }));

        Assert.Empty(await client.GetTransactionValuationsAsync(1, 1));
    }

    private static HttpClient CreateHttpClient(HttpResponseMessage response) =>
        new(new StubHandler(response)) { BaseAddress = new Uri("http://localhost/") };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}