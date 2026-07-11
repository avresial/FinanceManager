using FinanceManager.Api.Controllers;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class DevelopLoginControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        // The develop login is passwordless: the controller looks the seeded test user up by login only.
        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(x => x.GetUser(DevelopLoginController.TestUserLogin))
            .ReturnsAsync(new User
            {
                UserId = 42,
                Login = DevelopLoginController.TestUserLogin,
                UserRole = UserRole.User,
                CreationDate = DateTime.UtcNow
            });

        services.AddSingleton(userRepoMock.Object);
    }

    [Fact]
    public async Task DevelopLogin_AsGuest_IssuesEphemeralTokenWithGuestClaim()
    {
        var response = await Client.PostAsync("api/DevelopLogin/guest", null, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponseModel>(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        // Guest ids are minted as negative ints by the session store to avoid colliding with real identity-column ids.
        Assert.True(body.UserId < 0, $"Expected negative ephemeral guest id, got {body.UserId}");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Equal("true", jwt.Claims.SingleOrDefault(c => c.Type == "isGuest")?.Value);
    }

    [Fact]
    public async Task DevelopLogin_AsTestUser_ReturnsTokenAndRefreshCookie()
    {
        var response = await Client.PostAsync("api/DevelopLogin/testuser", null, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponseModel>(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal(DevelopLoginController.TestUserLogin, body.UserName);
        Assert.Equal(42, body.UserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));

        // Unlike guests, the test user gets a refresh cookie so the dev session survives page reloads.
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, c => c.StartsWith("fm_refresh_token=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DevelopLogin_UnknownLogin_ReturnsBadRequest()
    {
        var response = await Client.PostAsync("api/DevelopLogin/admin", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>
/// Boots the API with a production-like environment name to prove the develop login endpoint is absent
/// (404) there — the server-side gate the whole feature relies on. "Release" is used because, unlike
/// "Production", it has no appsettings overlay in the repo, so the host starts with the same test
/// configuration while still hitting the blocked-environment branch.
/// </summary>
[Trait("Category", "Integration")]
public class DevelopLoginControllerBlockedEnvironmentTests(OptionsProvider optionsProvider)
    : ControllerTests(optionsProvider, "Release")
{
    [Fact]
    public async Task DevelopLogin_InBlockedEnvironment_ReturnsNotFound()
    {
        var response = await Client.PostAsync("api/DevelopLogin/guest", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}