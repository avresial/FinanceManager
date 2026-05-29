using FinanceManager.Api.Services;
using FinanceManager.Api.Services.Guest;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Options;
using FinanceManager.Application.Providers;
using FinanceManager.Application.Services.Seeders;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Authentication")]
public class LoginController(JwtTokenGenerator jwtTokenGenerator, IUserRepository userRepository, IActiveUsersRepository activeUsersRepository,
    IGuestSessionStore guestSessionStore, IServiceScopeFactory scopeFactory,
    IInsightsGenerationChannel insightsGenerationChannel,
    IRefreshTokenService refreshTokenService, IOptions<RefreshTokenOptions> refreshOptions,
    ILogger<LoginController> logger) : ControllerBase
{
    private const string _guestLogin = "guest";

    [AllowAnonymous]
    [HttpPost(Name = "Login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginResponseModel))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequestModel requestModel, CancellationToken cancellationToken = default)
    {
        if (string.Equals(requestModel.UserName, _guestLogin, StringComparison.OrdinalIgnoreCase))
            return await LoginAsGuest(cancellationToken);

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(requestModel.Password);
        var user = await userRepository.GetUser(requestModel.UserName, encryptedPassword);

        if (user is null) return Forbid();

        var token = jwtTokenGenerator.GenerateToken(requestModel.UserName, user.UserId, user.UserRole);

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