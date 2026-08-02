using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Identity;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Identity.Services;

[Trait("Category", "Unit")]
public class LoginServiceTests
{
    [Fact]
    public async Task TryRefresh_SendsAntiforgeryHeader()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new LoginResponseModel
            {
                UserId = 1,
                UserName = "user@example.com",
                UserRole = UserRole.User,
                AccessToken = "fresh-token",
                ExpiresIn = 900,
            }),
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var antiforgery = new Mock<IAntiforgeryTokenService>();
        antiforgery
            .Setup(service => service.CreateRequest(HttpMethod.Post, "api/Auth/refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/Auth/refresh");
                request.Headers.TryAddWithoutValidation(IAntiforgeryTokenService.HeaderName, "csrf-token");
                return request;
            });

        var loginService = new LoginService(
            Mock.Of<ISessionStorageService>(),
            Mock.Of<ILocalStorageService>(),
            new CustomAuthenticationStateProvider(),
            httpClient,
            antiforgery.Object,
            NullLogger<LoginService>.Instance);

        var refreshed = await loginService.TryRefresh();

        Assert.True(refreshed);
        var request = Assert.Single(handler.Requests);
        Assert.True(request.Headers.TryGetValues(IAntiforgeryTokenService.HeaderName, out var values));
        Assert.Equal("csrf-token", Assert.Single(values));
        antiforgery.Verify(service => service.ClearToken(), Times.Once);
    }

    [Fact]
    public async Task TryRefresh_ConcurrentCalls_IssueASingleRefreshRequest()
    {
        var gate = new TaskCompletionSource();
        var handler = new GatedHandler(gate.Task, HttpStatusCode.OK);
        var loginService = CreateLoginService(handler);

        // Started synchronously so every caller queues on the refresh lock behind the one in-flight request.
        var refreshes = Enumerable.Range(0, 5).Select(_ => loginService.TryRefresh()).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(refreshes);

        Assert.Equal(1, handler.RequestCount);
        Assert.All(results, Assert.True);
    }

    [Fact]
    public async Task TryRefresh_ConcurrentCalls_AllObserveAFailedRefresh()
    {
        var gate = new TaskCompletionSource();
        var handler = new GatedHandler(gate.Task, HttpStatusCode.Unauthorized);
        var loginService = CreateLoginService(handler);

        var refreshes = Enumerable.Range(0, 5).Select(_ => loginService.TryRefresh()).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(refreshes);

        Assert.Equal(1, handler.RequestCount);
        Assert.All(results, result => Assert.False(result));
    }

    [Fact]
    public async Task TryRefresh_AfterAnEarlierRefreshCompleted_IssuesAFreshRequest()
    {
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var handler = new GatedHandler(gate.Task, HttpStatusCode.OK);
        var loginService = CreateLoginService(handler);

        Assert.True(await loginService.TryRefresh());
        Assert.True(await loginService.TryRefresh());

        // Coalescing applies to a burst of concurrent callers, not to sequential ones: a caller that arrives
        // after the previous refresh has settled must still get a freshly rotated token.
        Assert.Equal(2, handler.RequestCount);
    }

    private static LoginService CreateLoginService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var antiforgery = new Mock<IAntiforgeryTokenService>();
        antiforgery
            .Setup(service => service.CreateRequest(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HttpMethod method, string uri, CancellationToken _) => new HttpRequestMessage(method, uri));

        return new LoginService(
            Mock.Of<ISessionStorageService>(),
            Mock.Of<ILocalStorageService>(),
            new CustomAuthenticationStateProvider(),
            httpClient,
            antiforgery.Object,
            NullLogger<LoginService>.Instance);
    }

    // Counts requests and holds each one open until the gate completes, so a test can park the first refresh
    // in flight while the remaining callers pile up behind the refresh lock. A fresh response per request
    // keeps the callers from sharing (and disposing) a single content stream.
    private sealed class GatedHandler(Task gate, HttpStatusCode statusCode) : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            await gate;

            if (statusCode != HttpStatusCode.OK)
                return new HttpResponseMessage(statusCode);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponseModel
                {
                    UserId = 1,
                    UserName = "user@example.com",
                    UserRole = UserRole.User,
                    AccessToken = "fresh-token",
                    ExpiresIn = 900,
                }),
            };
        }
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(response);
        }
    }
}