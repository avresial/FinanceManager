using FinanceManager.Api.Services;
using FinanceManager.Api.Services.Guest;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Identity;
using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Authentication")]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public class LoginController(JwtTokenGenerator jwtTokenGenerator, IUserRepository userRepository, IActiveUsersRepository activeUsersRepository,
    IGuestSessionStore guestSessionStore, IServiceScopeFactory scopeFactory,
    IInsightsGenerationChannel insightsGenerationChannel,
    IRefreshTokenService refreshTokenService, IAccountLockoutService accountLockoutService,
    IOptions<RefreshTokenOptions> refreshOptions,
    ILogger<LoginController> logger) : ControllerBase
{
    private const string _guestLogin = "guest";
    private const string _lockedOutMessage =
        "This account is temporarily locked due to repeated failed login attempts. Please try again later.";

    [AllowAnonymous]
    [HttpPost(Name = "Login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginResponseModel))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequestModel requestModel, CancellationToken cancellationToken = default)
    {
        if (string.Equals(requestModel.UserName, _guestLogin, StringComparison.OrdinalIgnoreCase))
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while adding user to active users repository");
        }

        try
        {
            await insightsGenerationChannel.QueueUser(token.UserId, cancellationToken);
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while issuing a refresh token for user {UserId}", user.UserId);
        }

        return Ok(token);
    }

    private async Task<IActionResult> LoginAsGuest(CancellationToken cancellationToken)
    {
        var guestUserId = guestSessionStore.CreateSession();

        try
        {
            await SeedGuestSandbox(guestUserId, cancellationToken);
        }
        catch (Exception ex)
        {
            // A half-seeded sandbox would still authenticate (the session is in the store, the token would be valid),
            // and the guest would land in a broken-looking app. Roll the session back and surface a hard failure.
            logger.LogError(ex, "Failed to seed guest sandbox for {GuestUserId}; aborting guest login.", guestUserId);
            guestSessionStore.Remove(guestUserId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        var token = jwtTokenGenerator.GenerateToken(_guestLogin, guestUserId, UserRole.User, isGuest: true);
        return Ok(token);
    }

    // Seeding runs before the request principal carries an isGuest claim, so we open a fresh scope and pin the
    // ambient guest accessor to route AppDbContext to the per-session in-memory database.
    private async Task SeedGuestSandbox(int guestUserId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IGuestSessionAccessor>().SetGuestUserId(guestUserId);
        var seeder = scope.ServiceProvider.GetRequiredService<GuestAccountSeeder>();
        await seeder.SeedForGuest(guestUserId, cancellationToken);
    }
}