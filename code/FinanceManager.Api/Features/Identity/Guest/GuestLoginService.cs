using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;

namespace FinanceManager.Api.Features.Identity.Guest;

public class GuestLoginService(IGuestSessionStore guestSessionStore, IServiceScopeFactory scopeFactory,
    JwtTokenGenerator jwtTokenGenerator, ILogger<GuestLoginService> logger) : IGuestLoginService
{
    public const string GuestLogin = "guest";

    public async Task<LoginResponseModel?> LoginAsGuest(CancellationToken cancellationToken = default)
    {
        var guestUserId = guestSessionStore.CreateSession();

        try
        {
            await SeedGuestSandbox(guestUserId, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Guest sandbox seeding cancelled for {GuestUserId}; aborting guest login.", guestUserId);
            guestSessionStore.Remove(guestUserId);
            throw;
        }
        catch (Exception ex)
        {
            // A half-seeded sandbox would still authenticate (the session is in the store, the token would be valid),
            // and the guest would land in a broken-looking app. Roll the session back and surface a hard failure.
            logger.LogError(ex, "Failed to seed guest sandbox for {GuestUserId}; aborting guest login.", guestUserId);
            guestSessionStore.Remove(guestUserId);
            return null;
        }

        return jwtTokenGenerator.GenerateToken(GuestLogin, guestUserId, UserRole.User, isGuest: true);
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