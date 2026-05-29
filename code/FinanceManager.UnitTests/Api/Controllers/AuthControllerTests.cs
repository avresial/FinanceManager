using FinanceManager.Api.Controllers;
using FinanceManager.Api.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Options;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.UnitTests.Api.Controllers;

[Trait("Category", "Unit")]
public class AuthControllerTests
{
    private const string _cookieName = "fm_refresh_token";

    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly RefreshTokenOptions _options = new() { CookieName = _cookieName, SlidingValidityDays = 14 };
    private readonly JwtTokenGenerator _jwtTokenGenerator = new(Options.Create(new JwtAuthOptions
    {
        Issuer = "test",
        Audience = "test",
        Key = new string('k', 64),
        TokenValidityMins = 15,
    }));

    private AuthController CreateController(string? cookieValue, out HttpContext httpContext)
    {
        httpContext = new DefaultHttpContext();
        if (cookieValue is not null)
            httpContext.Request.Headers["Cookie"] = $"{_cookieName}={cookieValue}";

        return new AuthController(_refreshTokenService.Object, _jwtTokenGenerator, _userRepository.Object,
            Options.Create(_options), NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static bool HasSetCookie(HttpContext ctx, out string value)
    {
        value = ctx.Response.Headers.SetCookie.ToString();
        return !string.IsNullOrEmpty(value);
    }

    [Fact]
    public async Task Refresh_NoCookie_ReturnsUnauthorized()
    {
        var controller = CreateController(cookieValue: null, out _);

        var result = await controller.Refresh(TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result);
        _refreshTokenService.Verify(s => s.ValidateAndRotate(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsAccessTokenAndRotatesCookie()
    {
        _refreshTokenService.Setup(s => s.ValidateAndRotate("rawtoken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshTokenRotationResult.Ok(99, "newtoken"));
        _userRepository.Setup(r => r.GetUser(99)).ReturnsAsync(new User
        {
            UserId = 99,
            Login = "alice",
            UserRole = UserRole.User,
            CreationDate = DateTime.UtcNow,
        });

        var controller = CreateController("rawtoken", out var ctx);

        var result = await controller.Refresh(TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<LoginResponseModel>(ok.Value);
        Assert.Equal("alice", body.UserName);
        Assert.Equal(99, body.UserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));

        Assert.True(HasSetCookie(ctx, out var setCookie));
        Assert.Contains($"{_cookieName}=newtoken", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(RefreshTokenStatus.NotFound)]
    [InlineData(RefreshTokenStatus.Expired)]
    [InlineData(RefreshTokenStatus.Revoked)]
    public async Task Refresh_FailedRotation_ReturnsUnauthorizedAndClearsCookie(RefreshTokenStatus status)
    {
        _refreshTokenService.Setup(s => s.ValidateAndRotate("rawtoken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshTokenRotationResult.Failure(status));

        var controller = CreateController("rawtoken", out var ctx);

        var result = await controller.Refresh(TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result);
        // Clearing the cookie emits a Set-Cookie with an expired date.
        Assert.True(HasSetCookie(ctx, out var setCookie));
        Assert.Contains(_cookieName, setCookie);
        _userRepository.Verify(r => r.GetUser(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_UnknownUser_ReturnsUnauthorized()
    {
        _refreshTokenService.Setup(s => s.ValidateAndRotate("rawtoken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshTokenRotationResult.Ok(99, "newtoken"));
        _userRepository.Setup(r => r.GetUser(99)).ReturnsAsync((User?)null);

        var controller = CreateController("rawtoken", out _);

        var result = await controller.Refresh(TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Logout_WithCookie_RevokesTokenAndClearsCookie()
    {
        _refreshTokenService.Setup(s => s.Revoke("rawtoken", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var controller = CreateController("rawtoken", out var ctx);

        var result = await controller.Logout(TestContext.Current.CancellationToken);

        Assert.IsType<OkResult>(result);
        _refreshTokenService.Verify(s => s.Revoke("rawtoken", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(HasSetCookie(ctx, out var setCookie));
        Assert.Contains(_cookieName, setCookie);
    }

    [Fact]
    public async Task Logout_WithoutCookie_StillSucceeds()
    {
        var controller = CreateController(cookieValue: null, out _);

        var result = await controller.Logout(TestContext.Current.CancellationToken);

        Assert.IsType<OkResult>(result);
        _refreshTokenService.Verify(s => s.Revoke(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}