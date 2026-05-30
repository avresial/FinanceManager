using FinanceManager.Api.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Options;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Authentication")]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public class AuthController(
    IRefreshTokenService refreshTokenService,
    JwtTokenGenerator jwtTokenGenerator,
    IUserRepository userRepository,
    IOptions<RefreshTokenOptions> refreshOptions,
    ILogger<AuthController> logger) : ControllerBase
{
    private RefreshTokenOptions Options => refreshOptions.Value;

    /// <summary>
    /// Exchanges the refresh-token cookie for a fresh access token, rotating the refresh token in the process.
    /// Returns 401 (and clears the cookie) whenever the refresh token is missing, expired, or revoked.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginResponseModel))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken = default)
    {
        if (!Request.Cookies.TryGetValue(Options.CookieName, out var rawToken) || string.IsNullOrWhiteSpace(rawToken))
            return Unauthorized();

        var rotation = await refreshTokenService.ValidateAndRotate(rawToken, cancellationToken);
        if (rotation.Status != RefreshTokenStatus.Success)
        {
            if (rotation.Status == RefreshTokenStatus.Revoked)
                logger.LogWarning("A revoked refresh token was replayed; the token family has been revoked.");

            ClearCookie();
            return Unauthorized();
        }

        var user = await userRepository.GetUser(rotation.UserId);
        if (user is null)
        {
            ClearCookie();
            return Unauthorized();
        }

        var token = jwtTokenGenerator.GenerateToken(user.Login, user.UserId, user.UserRole);
        AppendCookie(rotation.NewRefreshToken!);
        return Ok(token);
    }

    /// <summary>Revokes the current refresh token server-side and clears the cookie.</summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        if (Request.Cookies.TryGetValue(Options.CookieName, out var rawToken) && !string.IsNullOrWhiteSpace(rawToken))
            await refreshTokenService.Revoke(rawToken, cancellationToken);

        ClearCookie();
        return Ok();
    }

    private void AppendCookie(string rawToken) =>
        RefreshTokenCookie.Append(Response, Request.IsHttps, Options.CookieName, rawToken,
            DateTimeOffset.UtcNow.AddDays(Options.SlidingValidityDays));

    private void ClearCookie() =>
        RefreshTokenCookie.Delete(Response, Request.IsHttps, Options.CookieName);
}