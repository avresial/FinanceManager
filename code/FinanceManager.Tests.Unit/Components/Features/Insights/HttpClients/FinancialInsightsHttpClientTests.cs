using FinanceManager.Components.Features.Insights.HttpClients;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Insights.HttpClients;

[Trait("Category", "Unit")]
public class FinancialInsightsHttpClientTests
{
    [Fact]
    public async Task GetLatestAsync_SuccessWithNoInsights_ReturnsEmptyList()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<object>())
        });

        var result = await client.GetLatestAsync(3, null, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLatestAsync_FailedRequest_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // The dashboard carousel caches what it renders, so a failure must not be reported as an
        // empty result — that would clear the card and overwrite its snapshot with nothing.
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetLatestAsync(3, null, TestContext.Current.CancellationToken));
    }

    private static FinancialInsightsHttpClient CreateClient(HttpResponseMessage response) =>
        new(new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("http://localhost/") });

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}