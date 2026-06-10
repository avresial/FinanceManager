using FinanceManager.Api.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Antiforgery;
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
    IAntiforgery antiforgery,
    ILogger<AuthController> logger) : ControllerBase
{
    public const string CsrfTokenCookieName = "fm_csrf";

    private RefreshTokenOptions Options => refreshOptions.Value;

    /// <summary>Issues an antiforgery request token that the Blazor client echoes in the X-CSRF-Token header.</summary>
    [AllowAnonymous]
    [HttpGet("csrf-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append(CsrfTokenCookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });

        return NoContent();
    }

    /// <summary>
    /// Exchanges the refresh-token cookie for a fresh access token, rotating the refresh token in the process.
    /// Returns 401 (and clears the cookie) whenever the refresh token is missing, expired, or revoked.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginResponseModel))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken = default)
    {
        if (!await ValidateAntiforgeryToken())
            return Forbid();

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        if (!await ValidateAntiforgeryToken())
            return Forbid();

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

    private async Task<bool> ValidateAntiforgeryToken()
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning(ex, "Rejected auth request with an invalid antiforgery token.");
            return false;
        }
    }
}