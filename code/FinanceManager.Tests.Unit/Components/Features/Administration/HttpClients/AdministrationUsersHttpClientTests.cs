using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Domain.Shared.Charting;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Administration.HttpClients;

[Trait("Category", "Unit")]
public class AdministrationUsersHttpClientTests
{
    [Fact]
    public async Task GetAccountsCount_Success_ReturnsCount()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(5)
        });

        Assert.Equal(5, await client.GetAccountsCount());
    }

    [Fact]
    public async Task GetAccountsCount_GenuinelyZero_ReturnsZero()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(0)
        });

        Assert.Equal(0, await client.GetAccountsCount());
    }

    [Fact]
    public async Task GetAccountsCount_FailedRequest_ReturnsNull()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // The admin dashboard snapshot surface relies on null to mean "no usable response" so a failed
        // refresh keeps the painted count instead of overwriting it with a fake zero.
        Assert.Null(await client.GetAccountsCount());
    }

    [Fact]
    public async Task GetUsersCount_Success_ReturnsCount()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(5)
        });

        Assert.Equal(5, await client.GetUsersCount());
    }

    [Fact]
    public async Task GetUsersCount_GenuinelyZero_ReturnsZero()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(0)
        });

        Assert.Equal(0, await client.GetUsersCount());
    }

    [Fact]
    public async Task GetUsersCount_FailedRequest_ReturnsNull()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // The admin dashboard snapshot surface relies on null to mean "no usable response" so a failed
        // refresh keeps the painted count instead of overwriting it with a fake zero.
        Assert.Null(await client.GetUsersCount());
    }

    [Fact]
    public async Task GetDailyActiveUsers_Success_ReturnsData()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<ChartEntryModel> { new(new DateTime(2026, 10, 1), 4) })
        });

        Assert.Equal(4, (await client.GetDailyActiveUsers()).Single().Value);
    }

    [Fact]
    public async Task GetDailyActiveUsers_FailedRequest_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(client.GetDailyActiveUsers);
    }

    [Fact]
    public async Task GetDailyActiveUsers_NullBody_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create((List<ChartEntryModel>?)null)
        });

        await Assert.ThrowsAsync<InvalidOperationException>(client.GetDailyActiveUsers);
    }

    private static AdministrationUsersHttpClient CreateClient(HttpResponseMessage response) =>
        new(new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("http://localhost/") });

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}