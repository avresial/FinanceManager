using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class AuthControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const string _userName = "refreshuser";
    private const string _password = "password";
    private const int _userId = 4242;

    protected override void ConfigureServices(IServiceCollection services)
    {
        var user = new User
        {
            UserId = _userId,
            Login = _userName,
            UserRole = UserRole.User,
            CreationDate = DateTime.UtcNow,
        };

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(x => x.GetUser(_userName, PasswordEncryptionProvider.EncryptPassword(_password)))
            .ReturnsAsync(user);
        userRepoMock.Setup(x => x.GetUser(_userId)).ReturnsAsync(user);
        services.AddSingleton(userRepoMock.Object);

        var activeUsersMock = new Mock<IActiveUsersRepository>();
        activeUsersMock.Setup(x => x.Add(It.IsAny<int>(), It.IsAny<DateOnly>())).Returns(Task.CompletedTask);
        services.AddSingleton(activeUsersMock.Object);

        var refreshTokenServiceMock = new Mock<IRefreshTokenService>();
        refreshTokenServiceMock.Setup(x => x.Issue(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token-1");
        refreshTokenServiceMock.Setup(x => x.ValidateAndRotate("refresh-token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshTokenRotationResult.Ok(_userId, "refresh-token-2"));
        refreshTokenServiceMock.Setup(x => x.Revoke("refresh-token-2", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton(refreshTokenServiceMock.Object);
    }

    [Fact]
    public async Task Login_Refresh_Logout_Flow()
    {
        var ct = TestContext.Current.CancellationToken;

        // Login sets the refresh-token cookie (handled automatically by the client's cookie container).
        var loginRequest = new LoginRequestModel(_userName, _password);
        var loginResponse = await Client.PostAsJsonAsync("api/Login", loginRequest, ct);
        loginResponse.EnsureSuccessStatusCode();
        var setCookie = loginResponse.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join(";", values)
            : string.Empty;
        Assert.Contains("fm_refresh_token", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/Auth", setCookie, StringComparison.OrdinalIgnoreCase);
        // Secure is intentionally NOT asserted here: the test client talks plain HTTP, and the cookie's Secure flag
        // tracks Request.IsHttps (verified over HTTPS in the AuthController unit tests instead).

        // Refresh exchanges the cookie for a new access token without any credentials.
        var refreshResponse = await Client.PostAsync("api/Auth/refresh", content: null, ct);
        refreshResponse.EnsureSuccessStatusCode();
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<LoginResponseModel>(ct);
        Assert.NotNull(refreshed);
        Assert.Equal(_userId, refreshed!.UserId);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));

        // Logout revokes the token server-side and clears the cookie.
        var logoutResponse = await Client.PostAsync("api/Auth/logout", content: null, ct);
        logoutResponse.EnsureSuccessStatusCode();

        // After logout there is no usable refresh cookie, so a further refresh is rejected.
        var afterLogout = await Client.PostAsync("api/Auth/refresh", content: null, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("api/Auth/refresh", content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}