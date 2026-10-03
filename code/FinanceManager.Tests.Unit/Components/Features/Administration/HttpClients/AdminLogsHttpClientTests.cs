using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Domain.Administration.Logging;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Administration.HttpClients;

[Trait("Category", "Unit")]
public class AdminLogsHttpClientTests
{
    [Fact]
    public async Task GetLatest_Success_ReturnsEntries()
    {
        var entry = new LogEntryDto(3, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), LogSeverity.Error, "Cat", "Boom", null, null, null);
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { entry }) });

        var result = await client.GetLatest();

        Assert.Equal(entry, Assert.Single(result));
    }

    [Fact]
    public async Task GetLatest_GenuinelyEmpty_ReturnsEmptyList()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<LogEntryDto>()) });

        Assert.Empty(await client.GetLatest());
    }

    [Fact]
    public async Task GetLatest_FailedRequest_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetLatest());
    }

    [Fact]
    public async Task GetLatest_NullBody_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetLatest());
    }

    private static AdminLogsHttpClient CreateClient(HttpResponseMessage response) =>
        new(new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("http://localhost/") });

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}