using FinanceManager.Components.Features.Transactions.Investments.HttpClients;
using FinanceManager.Contracts.Transactions.Investments;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Investments.HttpClients;

[Trait("Category", "Unit")]
public class InvestmentTradesHttpClientTests
{
    private static readonly DateOnly _day = new(2026, 3, 1);

    [Fact]
    public async Task CreateAsync_PostsToTheOwnerRoute_AndReturnsTheSavedTrade()
    {
        var saved = new InvestmentTransactionDto(9, 1, 4, InvestmentTransactionTypeDto.Buy, 2m, 10m, "PLN", _day, null, null);
        var handler = new RecordingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(saved) });
        var client = new InvestmentTradesHttpClient(handler.CreateClient());

        var result = await client.CreateAsync(new CreateInvestmentTradeRequest(4, InvestmentTransactionTypeDto.Buy, 2m, 10m, "PLN", _day), TestContext.Current.CancellationToken);

        Assert.Equal(9, result.Id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Post, "/api/transactions/investment"), (request.Method, request.PathAndQuery));
    }

    [Fact]
    public async Task UpdateAndDelete_UseThePutAndDeleteVerbsOnTheTradeRoute()
    {
        var handler = new RecordingHttpHandler();
        var client = new InvestmentTradesHttpClient(handler.CreateClient());

        await client.UpdateAsync(7, new UpdateInvestmentTradeRequest(4, InvestmentTransactionTypeDto.Sell, 1m, 12m, "PLN", _day), TestContext.Current.CancellationToken);
        await client.DeleteAsync(7, TestContext.Current.CancellationToken);

        Assert.Equal([(HttpMethod.Put, "/api/transactions/investment/7"), (HttpMethod.Delete, "/api/transactions/investment/7")],
            handler.Requests.Select(request => (request.Method, request.PathAndQuery)));
    }

    [Fact]
    public async Task ARejectedRequest_ThrowsWithTheServersExplanation()
    {
        var handler = new RecordingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create("Unknown trade type.") });
        var client = new InvestmentTradesHttpClient(handler.CreateClient());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.DeleteAsync(7, TestContext.Current.CancellationToken));

        Assert.Equal("Unknown trade type.", error.Message);
    }
}