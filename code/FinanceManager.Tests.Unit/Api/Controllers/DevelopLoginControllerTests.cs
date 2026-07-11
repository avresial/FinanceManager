using FinanceManager.Api.Controllers;
using FinanceManager.Api.Services;
using FinanceManager.Api.Services.Guest;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.Tests.Unit.Api.Controllers;

[Trait("Category", "Unit")]
public class DevelopLoginControllerTests
{
    private const string _cookieName = "fm_refresh_token";

    private readonly Mock<IHostEnvironment> _environment = new();
    private readonly Mock<IGuestLoginService> _guestLoginService = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly JwtTokenGenerator _jwtTokenGenerator = new(Options.Create(new JwtAuthOptions
    {
        Issuer = "test",
        Audience = "test",
        Key = new string('k', 64),
        TokenValidityMins = 15,
    }));

    private DevelopLoginController CreateController(string environmentName, out HttpContext httpContext)
    {
        _environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        httpContext = new DefaultHttpContext();

        return new DevelopLoginController(_environment.Object, _guestLoginService.Object, _jwtTokenGenerator,
            _userRepository.Object, _refreshTokenService.Object,
            Options.Create(new RefreshTokenOptions { CookieName = _cookieName, SlidingValidityDays = 14 }),
            NullLogger<DevelopLoginController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Release")]
    public async Task Login_BlockedEnvironment_ReturnsNotFound(string environmentName)
    {
        var controller = CreateController(environmentName, out _);

        var result = await controller.Login("guest", TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        _guestLoginService.Verify(s => s.LoginAsGuest(It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.GetUser(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public async Task Login_Guest_AllowedEnvironment_ReturnsGuestToken(string environmentName)
    {
        var controller = CreateController(environmentName, out _);
        var guestToken = new LoginResponseModel { UserName = "guest", UserRole = UserRole.User, UserId = 7, AccessToken = "token" };
        _guestLoginService.Setup(s => s.LoginAsGuest(It.IsAny<CancellationToken>())).ReturnsAsync(guestToken);

        var result = await controller.Login("Guest", TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(guestToken, ok.Value);
    }

    [Fact]
    public async Task Login_Guest_SeedingFails_ReturnsServerError()
    {
        var controller = CreateController("Development", out _);
        _guestLoginService.Setup(s => s.LoginAsGuest(It.IsAny<CancellationToken>())).ReturnsAsync((LoginResponseModel?)null);

        var result = await controller.Login("guest", TestContext.Current.CancellationToken);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task Login_TestUser_Seeded_ReturnsTokenAndRefreshCookie()
    {
        var controller = CreateController("Development", out var httpContext);
        _userRepository.Setup(r => r.GetUser(DevelopLoginController.TestUserLogin)).ReturnsAsync(new User
        {
            UserId = 42,
            Login = DevelopLoginController.TestUserLogin,
            CreationDate = DateTime.UtcNow,
        });
        _refreshTokenService.Setup(s => s.Issue(42, It.IsAny<CancellationToken>())).ReturnsAsync("raw-refresh-token");

        var result = await controller.Login("testuser", TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var token = Assert.IsType<LoginResponseModel>(ok.Value);
        Assert.Equal(42, token.UserId);
        Assert.Equal(DevelopLoginController.TestUserLogin, token.UserName);
        Assert.Contains(_cookieName, httpContext.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task Login_TestUser_RefreshIssueFails_StillReturnsToken()
    {
        var controller = CreateController("Development", out var httpContext);
        _userRepository.Setup(r => r.GetUser(DevelopLoginController.TestUserLogin)).ReturnsAsync(new User
        {
            UserId = 42,
            Login = DevelopLoginController.TestUserLogin,
            CreationDate = DateTime.UtcNow,
        });
        _refreshTokenService.Setup(s => s.Issue(42, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await controller.Login("testuser", TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result);
        Assert.DoesNotContain(_cookieName, httpContext.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task Login_TestUser_NotSeeded_ReturnsServiceUnavailable()
    {
        var controller = CreateController("Development", out _);
        _userRepository.Setup(r => r.GetUser(DevelopLoginController.TestUserLogin)).ReturnsAsync((User?)null);

        var result = await controller.Login("testuser", TestContext.Current.CancellationToken);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
        _refreshTokenService.Verify(s => s.Issue(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_UnknownLogin_ReturnsBadRequest()
    {
        var controller = CreateController("Development", out _);

        var result = await controller.Login("admin", TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        _guestLoginService.Verify(s => s.LoginAsGuest(It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.GetUser(It.IsAny<string>()), Times.Never);
    }
}