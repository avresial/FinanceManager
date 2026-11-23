using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Contracts.Transactions.Cash;
using System.Net;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Shared;

[Trait("Category", "Unit")]
public class OwnerTransactionsHttpResponseTests
{
    [Theory]
    [InlineData("\"Too many entries.\"", "Too many entries.")]
    [InlineData("{\"detail\":\"Plan limit reached.\",\"title\":\"Bad Request\"}", "Plan limit reached.")]
    [InlineData("{\"title\":\"One or more validation errors occurred.\",\"errors\":{\"Description\":[\"Description is too long.\"]}}", "Description is too long.")]
    [InlineData("{\"title\":\"Bad Request\"}", "Bad Request")]
    // Shapes the API does not document must not throw anything but the HTTP error.
    [InlineData("{\"title\":\"Bad Request\",\"errors\":{\"Description\":\"not an array\"}}", "Bad Request")]
    [InlineData("{\"title\":\"Bad Request\",\"errors\":\"flat\"}", "Bad Request")]
    [InlineData("\"   \"", "Bad Request")]
    [InlineData("{\"detail\":\" \",\"title\":\"Bad Request\"}", "Bad Request")]
    [InlineData("plain text, not json", "plain text, not json")]
    public async Task ARejectedCall_SurfacesTheReadableMessageOfTheBody(string body, string expected)
    {
        var handler = new RecordingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(body), ReasonPhrase = "Bad Request" });
        var client = new CashTransactionsHttpClient(handler.CreateClient());

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.CreateAsync(new CreateCashEntryRequest(0, DateTime.UtcNow, 1m, "x", null, null), TestContext.Current.CancellationToken));

        Assert.Equal(expected, ex.Message);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }
}