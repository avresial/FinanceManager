using Blazored.LocalStorage;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;

namespace FinanceManager.Tests.Unit.Components.Services;

[Trait("Category", "Unit")]
public class TokenRefreshRedirectHandlerTests
{
    [Fact]
    public async Task FailedRefresh_EndsSessionAndRedirectsToLogin()
    {
        var loginService = new Mock<ILoginService>();
        loginService.Setup(l => l.TryRefresh()).ReturnsAsync(false);

        var (handler, nav, invoker) = BuildHandler(loginService, new HttpResponseMessage(HttpStatusCode.Unauthorized));

        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/User/Get/1"), CancellationToken.None);

        // The original 401 is surfaced to the caller, but the local session must be torn down (in-memory state
        // included) so the app stops treating the user as authenticated, and we redirect to the login page.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        loginService.Verify(l => l.EndSession(), Times.Once);
        Assert.Contains("Login", nav.LastNavigatedUri);
    }

    [Fact]
    public async Task SuccessfulRefresh_RetriesWithNewTokenAndDoesNotEndSession()
    {
        var loginService = new Mock<ILoginService>();
        loginService.Setup(l => l.TryRefresh()).ReturnsAsync(true);
        loginService.Setup(l => l.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 1,
            UserName = "guest",
            Password = string.Empty,
            UserRole = UserRole.User,
            Token = "fresh-token",
        });

        var (handler, nav, invoker) = BuildHandler(loginService,
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            new HttpResponseMessage(HttpStatusCode.OK));
        var stub = (StubHandler)handler.InnerHandler!;

        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/User/Get/1"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
        Assert.Equal("fresh-token", stub.Requests[1].Headers.Authorization?.Parameter);
        loginService.Verify(l => l.EndSession(), Times.Never);
        Assert.Null(nav.LastNavigatedUri);
    }

    [Fact]
    public async Task Unauthorized_OnAuthEndpoint_DoesNotAttemptRefresh()
    {
        var loginService = new Mock<ILoginService>();

        var (_, nav, invoker) = BuildHandler(loginService, new HttpResponseMessage(HttpStatusCode.Unauthorized));

        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/Login"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        loginService.Verify(l => l.TryRefresh(), Times.Never);
        loginService.Verify(l => l.EndSession(), Times.Never);
        Assert.Null(nav.LastNavigatedUri);
    }

    private static (TokenRefreshRedirectHandler handler, TestNavigationManager nav, HttpMessageInvoker invoker) BuildHandler(
        Mock<ILoginService> loginService, params HttpResponseMessage[] responses)
    {
        var services = new ServiceCollection();
        services.AddSingleton(loginService.Object);

        var nav = new TestNavigationManager();
        var handler = new TokenRefreshRedirectHandler(services.BuildServiceProvider(), nav, Mock.Of<ILocalStorageService>())
        {
            InnerHandler = new StubHandler(new Queue<HttpResponseMessage>(responses)),
        };

        return (handler, nav, new HttpMessageInvoker(handler));
    }

    private sealed class StubHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public string? LastNavigatedUri { get; private set; }

        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, bool forceLoad) => LastNavigatedUri = uri;
    }
}