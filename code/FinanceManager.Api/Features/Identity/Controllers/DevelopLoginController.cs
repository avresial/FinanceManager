using FinanceManager.Api.Features.Identity.Guest;
using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinanceManager.Api.Features.Identity.Controllers;

/// <summary>
/// Passwordless auto test login for AI agents and developers (see <c>AGENTS.md</c>), driven by the
/// <c>/DevelopLogin/{login}</c> UI entry path. The endpoint answering 404 in Production/Release is the actual
/// security boundary — the Blazor page that calls it is only a convenience shell.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Tags("Authentication")]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public class DevelopLoginController(IHostEnvironment environment, IGuestLoginService guestLoginService,
    JwtTokenGenerator jwtTokenGenerator, IUserRepository userRepository,
    IRefreshTokenService refreshTokenService, IOptions<RefreshTokenOptions> refreshOptions,
    ILogger<DevelopLoginController> logger) : ControllerBase
{
    public const string TestUserLogin = "testuser";

    [AllowAnonymous]
    [HttpPost("{login}", Name = "DevelopLogin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginResponseModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Login(string login, CancellationToken cancellationToken = default)
    {
        // 404 (not 403) outside development-like environments so the endpoint is indistinguishable from a
        // route that was never deployed.
        if (environment.IsProduction() || environment.IsEnvironment("Release"))
            return NotFound();

        if (string.Equals(login, GuestLoginService.GuestLogin, StringComparison.OrdinalIgnoreCase))
        {
            var token = await guestLoginService.LoginAsGuest(cancellationToken);
            if (token is null)
            {
                logger.LogError("Guest develop login failed: the guest sandbox could not be seeded.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok(token);
        }

        if (string.Equals(login, TestUserLogin, StringComparison.OrdinalIgnoreCase))
            return await LoginAsTestUser(cancellationToken);

        return BadRequest($"Develop login supports only '{GuestLoginService.GuestLogin}' and '{TestUserLogin}'.");
    }

    private async Task<IActionResult> LoginAsTestUser(CancellationToken cancellationToken)
    {
        var user = await userRepository.GetUser(TestUserLogin);
        if (user is null)
            // The account comes from TestUserAccountSeeder, which only runs when 'Seeding:TestUserPassword' is
            // configured. Creating the user here instead would silently undo that opt-in.
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                $"The '{TestUserLogin}' account is not seeded. Configure 'Seeding:TestUserPassword' and restart, or use '{GuestLoginService.GuestLogin}'.");

        var token = jwtTokenGenerator.GenerateToken(user.Login, user.UserId, user.UserRole);

        // Issue a refresh cookie like the regular login path so the dev session survives page reloads.
        try
        {
            var refreshToken = await refreshTokenService.Issue(user.UserId, cancellationToken);
            RefreshTokenCookie.Append(Response, Request.IsHttps, refreshOptions.Value.CookieName, refreshToken,
                DateTimeOffset.UtcNow.AddDays(refreshOptions.Value.SlidingValidityDays));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while issuing a refresh token for user {UserId}", user.UserId);
        }

        return Ok(token);
    }
}