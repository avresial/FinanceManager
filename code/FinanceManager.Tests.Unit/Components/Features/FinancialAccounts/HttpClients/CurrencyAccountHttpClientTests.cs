using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.HttpClients;

[Trait("Category", "Unit")]
public class CurrencyAccountHttpClientTests
{
    [Fact]
    public async Task GetAvailableAccountsAsync_UserHasNoAccounts_ReturnsEmptyList()
    {
        // The API answers 404 for a user without currency accounts; that must read as "none", not as a failure.
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var accounts = await client.GetAvailableAccountsAsync();

        Assert.Empty(accounts);
    }

    [Fact]
    public async Task GetAvailableAccountsAsync_AccountsExist_ReturnsThem()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<AvailableAccount> { new(11, "Everyday") })
        });

        var accounts = await client.GetAvailableAccountsAsync();

        Assert.Equal("Everyday", Assert.Single(accounts).AccountName);
    }

    private static CurrencyAccountHttpClient CreateClient(HttpResponseMessage response) =>
        new(new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("http://localhost/") });

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; } = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Response);
    }
}