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
            CookieReader(sessionPresent: true),
            NullLogger<LoginService>.Instance);

        var refreshed = await loginService.TryRefresh();

        Assert.True(refreshed);
        var request = Assert.Single(handler.Requests);
        Assert.True(request.Headers.TryGetValues(IAntiforgeryTokenService.HeaderName, out var values));
        Assert.Equal("csrf-token", Assert.Single(values));

        // A refresh keeps the same identity, so the cached antiforgery token must survive it — clearing it would
        // force an avoidable csrf-token round-trip (and permit) on the next auth operation.
        antiforgery.Verify(service => service.ClearToken(), Times.Never);
    }

    [Fact]
    public async Task Login_ClearsAntiforgeryToken()
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

        var loginService = new LoginService(
            Mock.Of<ISessionStorageService>(),
            Mock.Of<ILocalStorageService>(),
            new CustomAuthenticationStateProvider(),
            httpClient,
            antiforgery.Object,
            CookieReader(sessionPresent: false),
            NullLogger<LoginService>.Instance);

        var success = await loginService.Login(new UserSession
        {
            UserId = 0,
            UserName = "user@example.com",
            Password = "password",
            UserRole = UserRole.User,
        });

        Assert.True(success);
        // A login changes identity, so the cached antiforgery token must be cleared.
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

    [Fact]
    public async Task GetLoggedUser_WithoutTheSessionPresenceCookie_DoesNotCallTheRefreshEndpoint()
    {
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var handler = new GatedHandler(gate.Task, HttpStatusCode.OK);
        var loginService = CreateLoginService(handler, CookieReader(sessionPresent: false), LegacyProbe(alreadyProbed: true));

        var loggedUser = await loginService.GetLoggedUser();

        // A signed-out visitor loading the login page must cost nothing against the auth rate-limit budget.
        Assert.Equal(0, handler.RequestCount);
        Assert.Null(loggedUser);
    }

    [Fact]
    public async Task GetLoggedUser_ForASessionIssuedBeforeTheHintExisted_StillRestoresIt()
    {
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var handler = new GatedHandler(gate.Task, HttpStatusCode.OK);

        // A refresh token issued before this change is valid for weeks but has no companion cookie. Trusting the
        // hint straight away would sign those users out; the one-time probe is what keeps them signed in.
        var loginService = CreateLoginService(handler, CookieReader(sessionPresent: false), LegacyProbe(alreadyProbed: false));

        var loggedUser = await loginService.GetLoggedUser();

        Assert.Equal(1, handler.RequestCount);
        Assert.NotNull(loggedUser);
    }

    [Fact]
    public async Task GetLoggedUser_RecordsTheLegacyProbeSoItRunsOnlyOncePerBrowser()
    {
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var handler = new GatedHandler(gate.Task, HttpStatusCode.OK);
        var localStorage = LegacyProbe(alreadyProbed: false);
        var cookieReader = CookieReader(sessionPresent: false);

        // First page load: nothing recorded yet, so the probe runs.
        await CreateLoginService(handler, cookieReader, localStorage).GetLoggedUser();
        Assert.Equal(1, handler.RequestCount);

        // A second page load is a fresh LoginService over the same browser storage. Only the recorded flag can
        // stop it probing again — the per-instance "already attempted" guard does not survive a reload, so
        // asserting on one instance would prove nothing about the once-per-browser claim.
        await CreateLoginService(handler, cookieReader, localStorage).GetLoggedUser();

        Assert.Equal(1, handler.RequestCount);
        Mock.Get(localStorage).Verify(
            storage => storage.SetItemAsync("legacySessionProbed", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLoggedUser_WithTheSessionPresenceCookie_RestoresTheSession()
    {
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var handler = new GatedHandler(gate.Task, HttpStatusCode.OK);
        var loginService = CreateLoginService(handler, CookieReader(sessionPresent: true));

        var loggedUser = await loginService.GetLoggedUser();

        Assert.Equal(1, handler.RequestCount);
        Assert.NotNull(loggedUser);
        Assert.Equal("user@example.com", loggedUser.UserName);
    }

    [Fact]
    public async Task GetLoggedUser_WhenTheCookieCannotBeRead_StillAttemptsTheRefresh()
    {
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var handler = new GatedHandler(gate.Task, HttpStatusCode.OK);

        var cookieReader = new Mock<IBrowserCookieReader>();
        cookieReader
            .Setup(service => service.Exists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("JS interop unavailable."));

        var loginService = CreateLoginService(handler, cookieReader.Object);

        var loggedUser = await loginService.GetLoggedUser();

        // Failing open matters more than saving the request: a readable-cookie failure must never strand a
        // signed-in user on the login page.
        Assert.Equal(1, handler.RequestCount);
        Assert.NotNull(loggedUser);
    }

    private static LoginService CreateLoginService(
        HttpMessageHandler handler,
        IBrowserCookieReader? cookieReader = null,
        ILocalStorageService? localStorage = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var antiforgery = new Mock<IAntiforgeryTokenService>();
        antiforgery
            .Setup(service => service.CreateRequest(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HttpMethod method, string uri, CancellationToken _) => new HttpRequestMessage(method, uri));

        return new LoginService(
            Mock.Of<ISessionStorageService>(),
            localStorage ?? LegacyProbe(alreadyProbed: true),
            new CustomAuthenticationStateProvider(),
            httpClient,
            antiforgery.Object,
            cookieReader ?? CookieReader(sessionPresent: true),
            NullLogger<LoginService>.Instance);
    }

    // The default across most tests is "migration already done", so the presence cookie alone decides. The stored
    // flag is stateful rather than fixed: recording the probe has to be observable to a later LoginService, since
    // that -- not the per-instance "already attempted" flag -- is what makes the probe once-per-browser.
    private static ILocalStorageService LegacyProbe(bool alreadyProbed)
    {
        var probed = alreadyProbed;
        var localStorage = new Mock<ILocalStorageService>();
        localStorage
            .Setup(storage => storage.ContainKeyAsync("legacySessionProbed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => probed);
        localStorage
            .Setup(storage => storage.SetItemAsync("legacySessionProbed", true, It.IsAny<CancellationToken>()))
            .Callback(() => probed = true)
            .Returns(ValueTask.CompletedTask);

        return localStorage.Object;
    }

    private static IBrowserCookieReader CookieReader(bool sessionPresent)
    {
        var reader = new Mock<IBrowserCookieReader>();
        reader
            .Setup(service => service.Exists(LoginService.SessionPresenceCookieName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionPresent);

        return reader.Object;
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