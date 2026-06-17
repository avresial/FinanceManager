using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Identity;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Services;

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
            Mock.Of<IUserRepository>(),
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