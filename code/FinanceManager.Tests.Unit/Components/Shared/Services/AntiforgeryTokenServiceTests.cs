using FinanceManager.Components.Shared.Services;
using Microsoft.JSInterop;
using Moq;
using System.Net;

namespace FinanceManager.Tests.Unit.Components.Shared.Services;

[Trait("Category", "Unit")]
public class AntiforgeryTokenServiceTests
{
    [Fact]
    public async Task CreateRequest_CachesTokenAndSerializesBootstrap()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var jsRuntime = new Mock<IJSRuntime>();
        jsRuntime
            .Setup(js => js.InvokeAsync<string>(
                "financeManager.getCookie",
                It.IsAny<CancellationToken>(),
                It.Is<object?[]>(args => args.Length == 1 && (string?)args[0] == AntiforgeryTokenService.CookieName)))
            .Returns(new ValueTask<string>("csrf-token"));

        var service = new AntiforgeryTokenService(httpClient, jsRuntime.Object);

        var requests = await Task.WhenAll(
            service.CreateRequest(HttpMethod.Post, "api/Auth/refresh", CancellationToken.None),
            service.CreateRequest(HttpMethod.Post, "api/Auth/logout", CancellationToken.None),
            service.CreateRequest(HttpMethod.Post, "api/Auth/refresh", CancellationToken.None));

        Assert.Equal(3, requests.Length);
        Assert.Equal(1, handler.RequestCount);
        jsRuntime.Verify(js => js.InvokeAsync<string>(
            "financeManager.getCookie",
            It.IsAny<CancellationToken>(),
            It.IsAny<object?[]>()), Times.Once);

        foreach (var request in requests)
        {
            Assert.True(request.Headers.TryGetValues(IAntiforgeryTokenService.HeaderName, out var values));
            Assert.Equal("csrf-token", Assert.Single(values));
        }
    }

    [Fact]
    public async Task ClearToken_ForcesNextRequestToBootstrapAgain()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var jsRuntime = new Mock<IJSRuntime>();
        jsRuntime
            .Setup(js => js.InvokeAsync<string>(
                "financeManager.getCookie",
                It.IsAny<CancellationToken>(),
                It.IsAny<object?[]>()))
            .Returns(new ValueTask<string>("csrf-token"));

        var service = new AntiforgeryTokenService(httpClient, jsRuntime.Object);

        using var first = await service.CreateRequest(HttpMethod.Post, "api/Auth/refresh", CancellationToken.None);
        service.ClearToken();
        using var second = await service.CreateRequest(HttpMethod.Post, "api/Auth/logout", CancellationToken.None);

        Assert.Equal(2, handler.RequestCount);
        jsRuntime.Verify(js => js.InvokeAsync<string>(
            "financeManager.getCookie",
            It.IsAny<CancellationToken>(),
            It.IsAny<object?[]>()), Times.Exactly(2));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            await Task.Delay(25, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}