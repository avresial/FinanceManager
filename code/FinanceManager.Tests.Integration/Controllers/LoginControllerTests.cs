using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Identity;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
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
public class LoginControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    // Stored login is lowercased (logins are normalized at the API boundary); see the mixed-case test below.
    private static readonly string _testUserName = "testuser@example.com";
    private static readonly string _testPassword = "password";
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(x => x.GetUser(_testUserName, PasswordEncryptionProvider.EncryptPassword(_testPassword)))
            .ReturnsAsync(new User
            {
                UserId = 99,
                Login = _testUserName,
                UserRole = UserRole.User,
                CreationDate = DateTime.UtcNow
            });

        services.AddSingleton(userRepoMock.Object);

        Mock<IActiveUsersRepository> activeUsersMock = new();
        activeUsersMock.Setup(x => x.Add(It.IsAny<int>(), It.IsAny<DateOnly>())).Returns(Task.CompletedTask);
        services.AddSingleton(activeUsersMock.Object);
    }

    [Fact]
    public async Task Login_ReturnsToken_ForValidCredentials()
    {
        // arrange
        // The client sends the plaintext password; the API hashes it exactly once before looking the user up.
        LoginRequestModel request = new(_testUserName, _testPassword);

        // act
        var response = await Client.PostAsJsonAsync("api/Login", request, cancellationToken: TestContext.Current.CancellationToken);

        // assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponseModel>(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal(_testUserName, body.UserName);
        Assert.Equal(99, body.UserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task Login_ReturnsToken_ForMixedCaseCredentials()
    {
        // arrange: post the username with different casing than the stored (lowercased) login. The API normalizes
        // the username before lookup, so this must still authenticate.
        LoginRequestModel request = new("TestUser@Example.com", _testPassword);

        // act
        var response = await Client.PostAsJsonAsync("api/Login", request, cancellationToken: TestContext.Current.CancellationToken);

        // assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponseModel>(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal(_testUserName, body.UserName);
        Assert.Equal(99, body.UserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("Guest")]
    [InlineData("GUEST")]
    public async Task Login_AsGuest_IssuesEphemeralTokenWithGuestClaim(string guestLogin)
    {
        LoginRequestModel request = new(guestLogin, "anything");

        var response = await Client.PostAsJsonAsync("api/Login", request, cancellationToken: TestContext.Current.CancellationToken);

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
    public async Task Login_AsGuest_AllocatesDistinctSandboxPerLogin()
    {
        var first = await Client.PostAsJsonAsync("api/Login", new LoginRequestModel("guest", "anything"), cancellationToken: TestContext.Current.CancellationToken);
        var second = await Client.PostAsJsonAsync("api/Login", new LoginRequestModel("guest", "anything"), cancellationToken: TestContext.Current.CancellationToken);

        var firstBody = await first.Content.ReadFromJsonAsync<LoginResponseModel>(cancellationToken: TestContext.Current.CancellationToken);
        var secondBody = await second.Content.ReadFromJsonAsync<LoginResponseModel>(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.NotEqual(firstBody.UserId, secondBody.UserId);
    }

    [Fact]
    public async Task Login_PreflightRequest_ReturnsCorsHeaders()
    {
        // arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "api/Login");
        request.Headers.Add("Origin", "https://localhost:7206");
        request.Headers.Add("Access-Control-Request-Method", HttpMethod.Post.Method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        // act
        using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://localhost:7206", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(HttpMethod.Post.Method, response.Headers.GetValues("Access-Control-Allow-Methods").Single(), StringComparison.OrdinalIgnoreCase);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}