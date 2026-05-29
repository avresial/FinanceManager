using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
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
        userRepoMock.Setup(x => x.GetUser(_userName, PasswordEncryptionProvider.EncryptPassword(PasswordEncryptionProvider.EncryptPassword(_password))))
            .ReturnsAsync(user);
        userRepoMock.Setup(x => x.GetUser(_userId)).ReturnsAsync(user);
        services.AddSingleton(userRepoMock.Object);

        var activeUsersMock = new Mock<IActiveUsersRepository>();
        activeUsersMock.Setup(x => x.Add(It.IsAny<int>(), It.IsAny<DateOnly>())).Returns(Task.CompletedTask);
        services.AddSingleton(activeUsersMock.Object);
    }

    [Fact]
    public async Task Login_Refresh_Logout_Flow()
    {
        var ct = TestContext.Current.CancellationToken;

        // Login sets the refresh-token cookie (handled automatically by the client's cookie container).
        var loginRequest = new LoginRequestModel(_userName, PasswordEncryptionProvider.EncryptPassword(_password));
        var loginResponse = await Client.PostAsJsonAsync("api/Login", loginRequest, ct);
        loginResponse.EnsureSuccessStatusCode();
        Assert.Contains(loginResponse.Headers, h => h.Key == "Set-Cookie" && h.Value.Any(v => v.Contains("fm_refresh_token")));

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