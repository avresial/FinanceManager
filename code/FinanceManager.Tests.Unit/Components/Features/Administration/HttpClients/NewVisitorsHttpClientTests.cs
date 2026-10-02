using FinanceManager.Components.Features.Administration.HttpClients;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Administration.HttpClients;

[Trait("Category", "Unit")]
public class NewVisitorsHttpClientTests
{
    [Fact]
    public async Task GetVisit_Success_ReturnsCount()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(5)
        });

        Assert.Equal(5, await client.GetVisit(DateTime.UtcNow));
    }

    [Fact]
    public async Task GetVisit_GenuinelyZero_ReturnsZero()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(0)
        });

        Assert.Equal(0, await client.GetVisit(DateTime.UtcNow));
    }

    [Fact]
    public async Task GetVisit_FailedRequest_ReturnsNull()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // The admin dashboard snapshot surface relies on null to mean "no usable response" so a failed
        // refresh keeps the painted visitors count instead of overwriting it with a fake zero.
        Assert.Null(await client.GetVisit(DateTime.UtcNow));
    }

    private static NewVisitorsHttpClient CreateClient(HttpResponseMessage response) =>
        new(new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("http://localhost/") }, NullLogger<NewVisitorsHttpClient>.Instance);

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}