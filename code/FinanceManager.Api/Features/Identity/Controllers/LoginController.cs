using FinanceManager.Api.Features.Identity.Guest;
using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Api.Features.Insights.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Identity;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinanceManager.Api.Features.Identity.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Authentication")]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public class LoginController(JwtTokenGenerator jwtTokenGenerator, IUserRepository userRepository, IActiveUsersRepository activeUsersRepository,
    IGuestLoginService guestLoginService,
    IInsightsGenerationChannel insightsGenerationChannel,
    IRefreshTokenService refreshTokenService, IAccountLockoutService accountLockoutService,
    IOptions<RefreshTokenOptions> refreshOptions,
    ILogger<LoginController> logger) : ControllerBase
{
    private const string _lockedOutMessage =
        "This account is temporarily locked due to repeated failed login attempts. Please try again later.";

    [AllowAnonymous]
    [HttpPost(Name = "Login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginResponseModel))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequestModel requestModel, CancellationToken cancellationToken = default)
    {
        if (string.Equals(requestModel.UserName, GuestLoginService.GuestLogin, StringComparison.OrdinalIgnoreCase))
            return await LoginAsGuest(cancellationToken);

        // Logins are case-insensitive emails stored lowercased at registration. Normalize here so a sign-in with
        // different casing — or a direct API caller that doesn't pre-lowercase like the Blazor client does — still
        // matches the stored row and shares a single lockout key.
        var userName = requestModel.UserName.ToLowerInvariant();

        // Per-account brute-force guard: refuse a locked account before touching its password so a flood of guesses
        // against one login can't keep checking credentials even when it rotates source IPs past the per-IP limiter.
        if (await accountLockoutService.IsLockedOut(userName, cancellationToken))
        {
            logger.LogWarning("Login refused for a locked-out account.");
            return StatusCode(StatusCodes.Status403Forbidden, _lockedOutMessage);
        }

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(requestModel.Password);
        var user = await userRepository.GetUser(userName, encryptedPassword);

        if (user is null)
        {
            await accountLockoutService.RegisterFailedAttempt(userName, cancellationToken);
            return Forbid();
        }

        await accountLockoutService.Reset(userName, cancellationToken);

        var token = jwtTokenGenerator.GenerateToken(userName, user.UserId, user.UserRole);

        try
        {
            await activeUsersRepository.Add(token.UserId, DateOnly.FromDateTime(DateTime.UtcNow));
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Adding user {UserId} to the active-user repository cancelled or timed out.", token.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while adding user to active users repository");
        }

        try
        {
            await insightsGenerationChannel.QueueUser(token.UserId, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Insights generation queueing cancelled for user {UserId}.", token.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while queueing user {UserId} for insights generation", token.UserId);
        }

        // Persist the session beyond the short-lived access token by issuing a rotating refresh token. Guests are
        // deliberately excluded below — their sandboxes are throwaway and stay on a single short-lived token.
        try
        {
            var refreshToken = await refreshTokenService.Issue(user.UserId, cancellationToken);
            RefreshTokenCookie.Append(Response, Request.IsHttps, refreshOptions.Value.CookieName, refreshToken,
                DateTimeOffset.UtcNow.AddDays(refreshOptions.Value.SlidingValidityDays));
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Refresh-token issuance cancelled for user {UserId}.", user.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while issuing a refresh token for user {UserId}", user.UserId);
        }

        return Ok(token);
    }

    private async Task<IActionResult> LoginAsGuest(CancellationToken cancellationToken)
    {
        var token = await guestLoginService.LoginAsGuest(cancellationToken);
        return token is null ? StatusCode(StatusCodes.Status500InternalServerError) : Ok(token);
    }
}